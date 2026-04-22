using Nskg.Data;
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
                .Where(x => x.DocNo == head.Vono &&
                            x.CompanyId == head.CompanyId &&
                            x.FinancialYearId == head.FinancialYearId);

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
                    ContraAcc = string.Join(",", details.Select(x => x.Acc)),

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
    }
}