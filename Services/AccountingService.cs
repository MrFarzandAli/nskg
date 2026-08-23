using Nskg.Data;
using Nskg.Helper;
using Nskg.Models;

namespace Nskg.Services
{
    public class AccountingService
    {
        private readonly ApplicationDbContext _context;

        public AccountingService(ApplicationDbContext context)
        {
            _context = context;
        }

        public void PostVoucher(VoHead head, List<VoDet> details)
        {
            // 🔥 Remove old posting
            var old = _context.GLTrans
                .Where(x => (x.RefId == head.Id || (x.DocNo == head.Vono && x.CompanyId == head.CompanyId && x.FinancialYearId == head.FinancialYearId)) &&
                            (x.RefType == "VO" || x.Votype == head.Votype || x.Votype == "TD"));

            _context.GLTrans.RemoveRange(old);

            decimal totalDr = 0;
            decimal totalCr = 0;
            int lineNo = 1;

            string? headerAcc = null;

            // 🔥 Get header account once
            if (head.gl3Id != null)
            {
                headerAcc = _context.GLChart3
                    .Where(x => x.Id == head.gl3Id)
                    .Select(x => x.ACC)
                    .FirstOrDefault();
            }

            // ✅ 1. DETAIL POSTING
            foreach (var d in details)
            {
                decimal dr = d.Dramt ?? 0;
                decimal cr = d.Cramt ?? 0;

                if (dr == 0 && cr == 0)
                    continue;

                totalDr += dr;
                totalCr += cr;

                _context.GLTrans.Add(new GLTrans
                {
                    DocNo = head.Vono,
                    Docdate = head.Vodate ?? DateTime.Now,
                    LineNo = lineNo++,

                    Accode = d.Acc,
                    ContraAcc = headerAcc,

                    Debit = dr,
                    Credit = cr,

                    CompanyId = head.CompanyId,
                    FinancialYearId = head.FinancialYearId,
                    Votype = head.Votype,
                    RefType = "VO",
                    RefId = head.Id,

                    Narration = d.Narration
                });

                // Tax Detection (PTAX)
                if ((d.Ptax ?? 0) != 0 && !string.IsNullOrEmpty(d.Acc))
                {
                    _context.GLTrans.Add(new GLTrans
                    {
                        DocNo = head.Vono,
                        Docdate = head.Vodate ?? DateTime.Now,
                        LineNo = lineNo++,

                        Accode = d.Acc,
                        ContraAcc = headerAcc,

                        Debit = 0,
                        Credit = Math.Abs(d.Ptax.Value),

                        CompanyId = head.CompanyId,
                        FinancialYearId = head.FinancialYearId,
                        Votype = "TD",
                        RefType = "VO",
                        RefId = head.Id,

                        Narration = "Tax Detection"
                    });
                }
            }

            // ✅ 2. HEADER POSTING
            if (!string.IsNullOrEmpty(headerAcc))
            {
                decimal headerDr = 0;
                decimal headerCr = 0;

                switch (head.Votype)
                {
                    case "CP":
                    case "BP":
                        headerCr = totalDr;
                        break;

                    case "CR":
                    case "BR":
                        headerDr = totalCr;
                        break;

                    case "JV":
                    default:
                        headerDr = totalCr;
                        headerCr = totalDr;
                        break;
                }

                _context.GLTrans.Add(new GLTrans
                {
                    DocNo = head.Vono,
                    Docdate = head.Vodate ?? DateTime.Now,
                    LineNo = lineNo,

                    Accode = headerAcc,
                    ContraAcc = string.Join(",", details.Where(x => !string.IsNullOrEmpty(x.Acc)).Select(x => x.Acc).Distinct()),

                    Debit = headerDr,
                    Credit = headerCr,

                    CompanyId = head.CompanyId,
                    FinancialYearId = head.FinancialYearId,
                    Votype = head.Votype,
                    RefType = "VO",
                    RefId = head.Id,

                    Narration = "Header Posting"
                });
            }

            _context.SaveChanges();
        }

        public void PostBilty(IssHead head, List<IssDetail> details)
        {
            // Remove old GL entries for this bilty
            var old = _context.GLTrans
                .Where(x => x.RefId == head.Id && (x.RefType == "BL" || x.RefType == "SL" || x.RefType == "WT") && x.CompanyId == head.CompanyId && x.FinancialYearId == head.FyId);
            _context.GLTrans.RemoveRange(old);

            int lineNo = 1;
            string cusAcc = head.CusCode ?? "";
            string salesAcc = head.AccCode ?? "";

            if (string.IsNullOrEmpty(salesAcc))
            {
                var salesAcPara = _context.AcPara
                    .FirstOrDefault(a => AccountCategories.Sales.Contains(a.ActypeCode)
                                         && a.CompanyId == head.CompanyId
                                         && a.Parent == "P");
                salesAcc = salesAcPara?.Accode ?? "";
            }

            // Calculate Net Amount
            decimal netAmt = head.NetAmt ?? (
                (head.Cartage1 ?? 0) + (head.Cartage2 ?? 0) + (head.Cartage3 ?? 0) +
                (head.Labour ?? 0) + (head.T_T ?? 0)
            );

            // If NetAmt is still 0, sum from details amount or rate * qty
            if (netAmt == 0 && details.Any())
            {
                netAmt = details.Sum(d => d.Amount ?? ((d.Qty ?? 0) * (d.Rate ?? 0)));
            }

            // 1. ITEM-WISE SALES TO CUSTOMER ACCOUNT (RefType 'BL', Votype 'BL')
            if (!string.IsNullOrEmpty(cusAcc) && netAmt > 0)
            {
                string itemDetails = details.Any()
                    ? string.Join(", ", details.Where(d => !string.IsNullOrEmpty(d.IName)).Select(d => $"{d.IName} (Qty: {d.Qty})"))
                    : "";

                string narration = !string.IsNullOrEmpty(head.InvNo?.ToString())
                    ? $"INV # {head.InvNo} {itemDetails}".Trim()
                    : (!string.IsNullOrEmpty(itemDetails) ? itemDetails : (head.Narration ?? $"Sales Bilty #{head.DocNo}"));

                _context.GLTrans.Add(new GLTrans
                {
                    DocNo = head.DocNo,
                    Docdate = head.DocDate ?? DateTime.Now,
                    LineNo = lineNo++,
                    Accode = cusAcc,
                    ContraAcc = salesAcc,
                    Debit = netAmt,
                    Credit = 0,
                    CompanyId = head.CompanyId,
                    FinancialYearId = head.FyId,
                    Votype = "BL",
                    RefType = "BL",
                    RefId = head.Id,
                    Narration = narration
                });
            }

            // 2. SALES ACCOUNT (RefType 'BL', Votype 'BL')
            if (!string.IsNullOrEmpty(salesAcc) && netAmt > 0)
            {
                decimal totalSTax = details.Sum(d => d.STaxAmt ?? 0) + (head.STaxAmt ?? 0);
                decimal cramt = netAmt + totalSTax;

                _context.GLTrans.Add(new GLTrans
                {
                    DocNo = head.DocNo,
                    Docdate = head.DocDate ?? DateTime.Now,
                    LineNo = lineNo++,
                    Accode = salesAcc,
                    ContraAcc = cusAcc,
                    Debit = 0,
                    Credit = cramt,
                    CompanyId = head.CompanyId,
                    FinancialYearId = head.FyId,
                    Votype = "BL",
                    RefType = "BL",
                    RefId = head.Id,
                    Narration = head.CusName ?? $"Sales Income Bilty #{head.DocNo}"
                });
            }

            // 3. CARTAGE PAYABLE TO DRIVERS / TRANSPORTERS (Votype 'SL', RefType 'SL')
            // Transporter 1
            if ((head.Cartage1 ?? 0) > 0 && !string.IsNullOrEmpty(head.Transporter))
            {
                _context.GLTrans.Add(new GLTrans
                {
                    DocNo = head.DocNo,
                    Docdate = head.DocDate ?? DateTime.Now,
                    LineNo = lineNo++,
                    Accode = head.Transporter,
                    ContraAcc = cusAcc,
                    Debit = 0,
                    Credit = head.Cartage1.Value,
                    CompanyId = head.CompanyId,
                    FinancialYearId = head.FyId,
                    Votype = "SL",
                    RefType = "SL",
                    RefId = head.Id,
                    Narration = $"CARTAGE on Inv #: {head.InvNo} {head.CartType} Trip to Customer: {head.CusName}"
                });
            }

            // Transporter 2
            if ((head.Cartage2 ?? 0) > 0 && !string.IsNullOrEmpty(head.Transporter2))
            {
                _context.GLTrans.Add(new GLTrans
                {
                    DocNo = head.DocNo,
                    Docdate = head.DocDate ?? DateTime.Now,
                    LineNo = lineNo++,
                    Accode = head.Transporter2,
                    ContraAcc = cusAcc,
                    Debit = 0,
                    Credit = head.Cartage2.Value,
                    CompanyId = head.CompanyId,
                    FinancialYearId = head.FyId,
                    Votype = "SL",
                    RefType = "SL",
                    RefId = head.Id,
                    Narration = $"CARTAGE on Inv #: {head.InvNo} {head.CartType2} Trip to Customer: {head.CusName}"
                });
            }

            // Transporter 3
            if ((head.Cartage3 ?? 0) > 0 && !string.IsNullOrEmpty(head.Transporter3))
            {
                _context.GLTrans.Add(new GLTrans
                {
                    DocNo = head.DocNo,
                    Docdate = head.DocDate ?? DateTime.Now,
                    LineNo = lineNo++,
                    Accode = head.Transporter3,
                    ContraAcc = cusAcc,
                    Debit = 0,
                    Credit = head.Cartage3.Value,
                    CompanyId = head.CompanyId,
                    FinancialYearId = head.FyId,
                    Votype = "SL",
                    RefType = "SL",
                    RefId = head.Id,
                    Narration = $"CARTAGE on Inv #: {head.InvNo} {head.CartType3} Trip to Customer: {head.CusName}"
                });
            }

            // 4. WITHHOLDING INCOME TAX (Votype 'WT', RefType 'WT')
            if (head.WH_IT_Rec != null && (head.WH_IT_Amt ?? 0) > 0 && !string.IsNullOrEmpty(cusAcc))
            {
                string itAcc = !string.IsNullOrEmpty(head.WH_IT_Acc) ? head.WH_IT_Acc : "051007";

                // Credit Customer
                _context.GLTrans.Add(new GLTrans
                {
                    DocNo = head.DocNo,
                    Docdate = head.WH_IT_Rec.Value,
                    LineNo = lineNo++,
                    Accode = cusAcc,
                    ContraAcc = itAcc,
                    Debit = 0,
                    Credit = head.WH_IT_Amt.Value,
                    CompanyId = head.CompanyId,
                    FinancialYearId = head.FyId,
                    Votype = "WT",
                    RefType = "WT",
                    RefId = head.Id,
                    Narration = $"WITH-HOLDING INCOME TAX ON SALES INVOICE # {head.InvNo} @ {head.WH_IT}%"
                });

                // Debit Tax Account
                _context.GLTrans.Add(new GLTrans
                {
                    DocNo = head.DocNo,
                    Docdate = head.WH_IT_Rec.Value,
                    LineNo = lineNo++,
                    Accode = itAcc,
                    ContraAcc = cusAcc,
                    Debit = head.WH_IT_Amt.Value,
                    Credit = 0,
                    CompanyId = head.CompanyId,
                    FinancialYearId = head.FyId,
                    Votype = "WT",
                    RefType = "WT",
                    RefId = head.Id,
                    Narration = $"W/H I.TAX ON INV.# {head.InvNo} @ {head.WH_IT}% Cus. {head.CusName}"
                });
            }

            // 5. WITHHOLDING SALES TAX (Votype 'WT', RefType 'WT')
            if (head.WH_ST_Rec != null && (head.WH_ST_Amt ?? 0) > 0 && !string.IsNullOrEmpty(cusAcc))
            {
                string stAcc = !string.IsNullOrEmpty(head.WH_ST_Acc) ? head.WH_ST_Acc : "053001";

                // Credit Customer
                _context.GLTrans.Add(new GLTrans
                {
                    DocNo = head.DocNo,
                    Docdate = head.WH_ST_Rec.Value,
                    LineNo = lineNo++,
                    Accode = cusAcc,
                    ContraAcc = stAcc,
                    Debit = 0,
                    Credit = head.WH_ST_Amt.Value,
                    CompanyId = head.CompanyId,
                    FinancialYearId = head.FyId,
                    Votype = "WT",
                    RefType = "WT",
                    RefId = head.Id,
                    Narration = $"WITH-HOLDING SALES TAX ON SALES INVOICE # {head.InvNo} @ {head.WH_ST}%"
                });

                // Debit Tax Account
                _context.GLTrans.Add(new GLTrans
                {
                    DocNo = head.DocNo,
                    Docdate = head.WH_ST_Rec.Value,
                    LineNo = lineNo++,
                    Accode = stAcc,
                    ContraAcc = cusAcc,
                    Debit = head.WH_ST_Amt.Value,
                    Credit = 0,
                    CompanyId = head.CompanyId,
                    FinancialYearId = head.FyId,
                    Votype = "WT",
                    RefType = "WT",
                    RefId = head.Id,
                    Narration = $"WITH-HOLDING SALES TAX ON SALES INVOICE # {head.InvNo} @ {head.WH_ST}%"
                });
            }

            _context.SaveChanges();
        }

        public void PostChallan(ChallanHead head, List<ChallanDet> details)
        {
            // Remove old GL entries for this challan
            var old = _context.GLTrans
                .Where(x => x.RefId == head.Id && x.RefType == "CL" && x.CompanyId == head.CompanyId && x.FinancialYearId == head.FyId);
            _context.GLTrans.RemoveRange(old);

            int lineNo = 1;

            // 1. Party Station Expenses (Debit PartyStationCode / Credit Expense Code)
            if (!string.IsNullOrEmpty(head.PartyStationCode))
            {
                if ((head.PExpAmt ?? 0) > 0)
                {
                    _context.GLTrans.Add(new GLTrans
                    {
                        DocNo = head.DocNo,
                        Docdate = head.DocDate ?? DateTime.Now,
                        LineNo = lineNo++,
                        Accode = head.PartyStationCode,
                        ContraAcc = head.PExpCode,
                        Debit = head.PExpAmt.Value,
                        Credit = 0,
                        CompanyId = head.CompanyId,
                        FinancialYearId = head.FyId,
                        Votype = "CL",
                        RefType = "CL",
                        RefId = head.Id,
                        Narration = $"Challan No. {head.ChalNo} Veh: {head.VehicleNo}"
                    });
                }
                if ((head.PExpAmt2 ?? 0) > 0)
                {
                    _context.GLTrans.Add(new GLTrans
                    {
                        DocNo = head.DocNo,
                        Docdate = head.DocDate ?? DateTime.Now,
                        LineNo = lineNo++,
                        Accode = head.PartyStationCode,
                        ContraAcc = head.PExpCode2,
                        Debit = head.PExpAmt2.Value,
                        Credit = 0,
                        CompanyId = head.CompanyId,
                        FinancialYearId = head.FyId,
                        Votype = "CL",
                        RefType = "CL",
                        RefId = head.Id,
                        Narration = $"Challan No. {head.ChalNo} Veh: {head.VehicleNo}"
                    });
                }
                if ((head.PExpAmt3 ?? 0) > 0)
                {
                    _context.GLTrans.Add(new GLTrans
                    {
                        DocNo = head.DocNo,
                        Docdate = head.DocDate ?? DateTime.Now,
                        LineNo = lineNo++,
                        Accode = head.PartyStationCode,
                        ContraAcc = head.PExpCode3,
                        Debit = head.PExpAmt3.Value,
                        Credit = 0,
                        CompanyId = head.CompanyId,
                        FinancialYearId = head.FyId,
                        Votype = "CL",
                        RefType = "CL",
                        RefId = head.Id,
                        Narration = $"Challan No. {head.ChalNo} Veh: {head.VehicleNo}"
                    });
                }
            }

            // 2. Corresponding Credit Entries for Expense Codes (PExpCode 1, 2, 3)
            if ((head.PExpAmt ?? 0) > 0 && !string.IsNullOrEmpty(head.PExpCode))
            {
                _context.GLTrans.Add(new GLTrans
                {
                    DocNo = head.DocNo,
                    Docdate = head.DocDate ?? DateTime.Now,
                    LineNo = lineNo++,
                    Accode = head.PExpCode,
                    ContraAcc = head.PartyStationCode,
                    Debit = 0,
                    Credit = head.PExpAmt.Value,
                    CompanyId = head.CompanyId,
                    FinancialYearId = head.FyId,
                    Votype = "CL",
                    RefType = "CL",
                    RefId = head.Id,
                    Narration = $"{head.Station} Challan No.{head.ChalNo}"
                });
            }
            if ((head.PExpAmt2 ?? 0) > 0 && !string.IsNullOrEmpty(head.PExpCode2))
            {
                _context.GLTrans.Add(new GLTrans
                {
                    DocNo = head.DocNo,
                    Docdate = head.DocDate ?? DateTime.Now,
                    LineNo = lineNo++,
                    Accode = head.PExpCode2,
                    ContraAcc = head.PartyStationCode,
                    Debit = 0,
                    Credit = head.PExpAmt2.Value,
                    CompanyId = head.CompanyId,
                    FinancialYearId = head.FyId,
                    Votype = "CL",
                    RefType = "CL",
                    RefId = head.Id,
                    Narration = $"{head.Station} Challan No.{head.ChalNo}"
                });
            }
            if ((head.PExpAmt3 ?? 0) > 0 && !string.IsNullOrEmpty(head.PExpCode3))
            {
                _context.GLTrans.Add(new GLTrans
                {
                    DocNo = head.DocNo,
                    Docdate = head.DocDate ?? DateTime.Now,
                    LineNo = lineNo++,
                    Accode = head.PExpCode3,
                    ContraAcc = head.PartyStationCode,
                    Debit = 0,
                    Credit = head.PExpAmt3.Value,
                    CompanyId = head.CompanyId,
                    FinancialYearId = head.FyId,
                    Votype = "CL",
                    RefType = "CL",
                    RefId = head.Id,
                    Narration = $"{head.Station} Challan No.{head.ChalNo}"
                });
            }

            _context.SaveChanges();
        }

        public void PostCommBook(CommHead head, List<CommDetail> details)
        {
            // Remove old GL entries for this commission book
            var old = _context.GLTrans
                .Where(x => x.RefId == (int)head.Id && (x.RefType == "CB" || x.RefType == "TR" || x.RefType == "LB" || x.RefType == "MU") && x.CompanyId == head.CompanyId && x.FinancialYearId == head.FinancialYearId);
            _context.GLTrans.RemoveRange(old);

            int lineNo = 1;

            string commSalesAcc = head.AcCode ?? "";
            if (string.IsNullOrEmpty(commSalesAcc))
            {
                var salesAcPara = _context.AcPara
                    .FirstOrDefault(a => AccountCategories.Sales.Contains(a.ActypeCode)
                                         && a.CompanyId == head.CompanyId
                                         && a.Parent == "P");
                commSalesAcc = salesAcPara?.Accode ?? "";
            }

            // 1. Transporter Account (RefType TR)
            if (!string.IsNullOrEmpty(head.TransCode) && (head.TransporterAmt ?? 0) > 0)
            {
                _context.GLTrans.Add(new GLTrans
                {
                    DocNo = head.DocNo,
                    Docdate = head.DocDate,
                    LineNo = lineNo++,
                    Accode = head.TransCode,
                    ContraAcc = commSalesAcc,
                    Debit = head.TransporterAmt.Value,
                    Credit = 0,
                    CompanyId = head.CompanyId,
                    FinancialYearId = head.FinancialYearId,
                    Votype = "TR",
                    RefType = "TR",
                    RefId = (int)head.Id,
                    Narration = $"Transporter Charges - Veh: {head.VehicleNo} Station: {head.Station}"
                });
            }

            // 2. Sales / Commission Account (RefType CB)
            if (!string.IsNullOrEmpty(commSalesAcc))
            {
                decimal totAmt = head.TotAmt ?? 0;
                if (totAmt > 0)
                {
                    _context.GLTrans.Add(new GLTrans
                    {
                        DocNo = head.DocNo,
                        Docdate = head.DocDate,
                        LineNo = lineNo++,
                        Accode = commSalesAcc,
                        ContraAcc = head.TransCode,
                        Debit = 0,
                        Credit = totAmt,
                        CompanyId = head.CompanyId,
                        FinancialYearId = head.FinancialYearId,
                        Votype = "CB",
                        RefType = "CB",
                        RefId = (int)head.Id,
                        Narration = $"Commission Head - Veh: {head.VehicleNo} Station: {head.Station}"
                    });
                }
                else if (totAmt < 0)
                {
                    _context.GLTrans.Add(new GLTrans
                    {
                        DocNo = head.DocNo,
                        Docdate = head.DocDate,
                        LineNo = lineNo++,
                        Accode = commSalesAcc,
                        ContraAcc = head.TransCode,
                        Debit = Math.Abs(totAmt),
                        Credit = 0,
                        CompanyId = head.CompanyId,
                        FinancialYearId = head.FinancialYearId,
                        Votype = "CB",
                        RefType = "CB",
                        RefId = (int)head.Id,
                        Narration = $"Commission Head - Veh: {head.VehicleNo} Station: {head.Station}"
                    });
                }
            }

            // 3. Labour Cartage in Sales (LB)
            if ((head.Labour ?? 0) > 0)
            {
                var labourAcPara = _context.AcPara
                    .FirstOrDefault(a => a.ActypeCode == "E" && a.CompanyId == head.CompanyId && a.Parent == "P");
                if (labourAcPara != null && !string.IsNullOrEmpty(labourAcPara.Accode))
                {
                    _context.GLTrans.Add(new GLTrans
                    {
                        DocNo = head.DocNo,
                        Docdate = head.DocDate,
                        LineNo = lineNo++,
                        Accode = labourAcPara.Accode,
                        ContraAcc = commSalesAcc,
                        Debit = 0,
                        Credit = head.Labour.Value,
                        CompanyId = head.CompanyId,
                        FinancialYearId = head.FinancialYearId,
                        Votype = "LB",
                        RefType = "LB",
                        RefId = (int)head.Id,
                        Narration = $"Labour Charges - Veh: {head.VehicleNo} Station: {head.Station}"
                    });
                }
            }

            // 4. Local Cartage (MU)
            if ((head.LocalAmt ?? 0) > 0)
            {
                var localAcPara = _context.AcPara
                    .FirstOrDefault(a => a.ActypeCode == "U" && a.CompanyId == head.CompanyId && a.Parent == "P");
                if (localAcPara != null && !string.IsNullOrEmpty(localAcPara.Accode))
                {
                    _context.GLTrans.Add(new GLTrans
                    {
                        DocNo = head.DocNo,
                        Docdate = head.DocDate,
                        LineNo = lineNo++,
                        Accode = localAcPara.Accode,
                        ContraAcc = commSalesAcc,
                        Debit = 0,
                        Credit = head.LocalAmt.Value,
                        CompanyId = head.CompanyId,
                        FinancialYearId = head.FinancialYearId,
                        Votype = "MU",
                        RefType = "MU",
                        RefId = (int)head.Id,
                        Narration = $"Local Cartage - Veh: {head.VehicleNo} Station: {head.Station}"
                    });
                }
            }

            _context.SaveChanges();
        }
    }
}
