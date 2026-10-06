using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Nskg.Data;
using Nskg.Extensions;
using Nskg.Models.ViewModels;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace Nskg.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _config;

        public DashboardController(ApplicationDbContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        public IActionResult Index(int? companyId, int? fyId)
        {
            return RedirectToAction(nameof(Financial), new { companyId, fyId });
        }

        public IActionResult Financial(int? companyId, int? fyId)
        {
            var vm = new FinancialDashboardViewModel();
            string connString = _config.GetConnectionString("DefaultConnection") ?? "";

            // 1. Companies & Financial Years dropdowns
            var companies = _context.Companies
                .Where(c => !c.IsDeleted)
                .OrderBy(c => c.Cocode)
                .Select(c => new DashboardCompanyItem
                {
                    Id = c.Id,
                    Code = c.Cocode ?? c.Id.ToString(),
                    Name = (!string.IsNullOrEmpty(c.Cocode) ? c.Cocode + " - " : "") + c.Name
                })
                .ToList();

            // Resolve selected company & financial year
            int defaultCompanyId = User.GetCompanyId();
            int selectedCompanyId = companyId ?? (defaultCompanyId > 0 ? defaultCompanyId : 0);
            vm.SelectedCompanyId = selectedCompanyId;

            var fyQuery = _context.FinancialYears
                .Where(f => !f.IsDeleted && !string.IsNullOrEmpty(f.YearName));
            if (selectedCompanyId > 0)
            {
                fyQuery = fyQuery.Where(f => f.CompanyId == selectedCompanyId);
            }

            var financialYears = fyQuery
                .OrderByDescending(f => !f.IsClosed)
                .ThenByDescending(f => f.StartDate)
                .Select(f => new DashboardFyItem
                {
                    Id = f.Id,
                    YearName = f.YearName,
                    StartDate = f.StartDate,
                    EndDate = f.EndDate,
                    IsClosed = f.IsClosed
                })
                .ToList();

            vm.Companies = companies;
            vm.FinancialYears = financialYears;

            var selectedComp = companies.FirstOrDefault(c => c.Id == selectedCompanyId);
            vm.SelectedCompanyName = selectedComp != null ? selectedComp.Name : "All Companies / Branches";
            string selectedCoCode = selectedComp?.Code ?? "";

            var defaultFy = financialYears.FirstOrDefault(f => !f.IsClosed) ?? financialYears.FirstOrDefault();
            int selectedFyId = fyId ?? (User.GetFinancialYearId() > 0 ? User.GetFinancialYearId() : (defaultFy?.Id ?? 4));
            vm.SelectedFyId = selectedFyId;

            var currentFyObj = financialYears.FirstOrDefault(f => f.Id == selectedFyId) ?? defaultFy;
            vm.SelectedFyName = currentFyObj?.YearName ?? "Current FY";
            DateTime fyStartDate = currentFyObj?.StartDate ?? new DateTime(DateTime.Now.Year, 7, 1);
            DateTime fyEndDate = currentFyObj?.EndDate ?? new DateTime(DateTime.Now.Year + 1, 6, 30);

            // 2. Process Trial Balance to keep GLChart balances fresh
            try
            {
                using (SqlConnection con = new SqlConnection(connString))
                {
                    con.Open();
                    using (SqlCommand cmdProc = new SqlCommand("dbo.sp_ProcessTrialBalance", con))
                    {
                        cmdProc.CommandType = CommandType.StoredProcedure;
                        cmdProc.CommandTimeout = 90;
                        cmdProc.Parameters.AddWithValue("@Cocode", string.IsNullOrWhiteSpace(selectedCoCode) ? (object)DBNull.Value : selectedCoCode.Trim());
                        cmdProc.Parameters.AddWithValue("@CompanyId", selectedCompanyId > 0 ? (object)selectedCompanyId : DBNull.Value);
                        cmdProc.Parameters.AddWithValue("@FinancialYearId", selectedFyId > 0 ? (object)selectedFyId : DBNull.Value);
                        cmdProc.Parameters.AddWithValue("@SDate", fyStartDate);
                        cmdProc.Parameters.AddWithValue("@TDate", fyEndDate);
                        cmdProc.ExecuteNonQuery();
                    }
                }
            }
            catch
            {
                // Proceed if SP is locked or encounters minor warning
            }

            // 3. Fast SQL Query for Aggregated Balances & Metrics
            try
            {
                using (SqlConnection con = new SqlConnection(connString))
                {
                    con.Open();

                    string sql = @"
                        -- Cash Balance (AC1 = '003' from VoHead and VoDet)
                        SELECT 
                            ISNULL((SELECT SUM(ISNULL(hdramt, 0) - ISNULL(hcramt, 0)) FROM VoHead WHERE IsDeleted = 0 AND votype <> 'JV' AND ac1 = '003' AND (@CompanyId = 0 OR CompanyId = @CompanyId)), 0)
                          + ISNULL((SELECT SUM(ISNULL(dramt, 0) - ISNULL(cramt, 0)) FROM VoDet WHERE IsDeleted = 0 AND ac1 = '003' AND (@CompanyId = 0 OR (COCODE = (SELECT TOP 1 Cocode FROM Companies WHERE Id = @CompanyId) OR COCODE = CAST(@CompanyId AS VARCHAR)))), 0);

                        -- Bank Balance (AC1 = '004' from VoHead and VoDet)
                        SELECT 
                            ISNULL((SELECT SUM(ISNULL(hdramt, 0) - ISNULL(hcramt, 0)) FROM VoHead WHERE IsDeleted = 0 AND votype <> 'JV' AND ac1 = '004' AND (@CompanyId = 0 OR CompanyId = @CompanyId)), 0)
                          + ISNULL((SELECT SUM(ISNULL(dramt, 0) - ISNULL(cramt, 0)) FROM VoDet WHERE IsDeleted = 0 AND ac1 = '004' AND (@CompanyId = 0 OR (COCODE = (SELECT TOP 1 Cocode FROM Companies WHERE Id = @CompanyId) OR COCODE = CAST(@CompanyId AS VARCHAR)))), 0);

                        -- Total Receivables (AC1 = '002' / Customer group with non-zero balance)
                        SELECT ISNULL(SUM(ABS(Opening)), 0) FROM GLChart3 
                        WHERE AC1 = '002' AND Opening <> 0 AND (@CompanyId = 0 OR CompanyId = @CompanyId);

                        -- Total Payables (AC1 = '052' / Transporters / Creditors)
                        SELECT ISNULL(SUM(ABS(Opening)), 0) FROM GLChart3 
                        WHERE AC1 = '052' AND Opening <> 0 AND (@CompanyId = 0 OR CompanyId = @CompanyId);

                        -- Advances (AC1 = '059')
                        SELECT ISNULL(SUM(ABS(Opening)), 0) FROM GLChart3 
                        WHERE AC1 = '059' AND Opening <> 0 AND (@CompanyId = 0 OR CompanyId = @CompanyId);

                        -- Pending To-Pay Bilties
                        SELECT COUNT(*), ISNULL(SUM(NetAmt), 0) 
                        FROM ISSHEAD 
                        WHERE PType = 'ToPay' AND IsDeleted = 0 
                          AND (@CompanyId = 0 OR CompanyId = @CompanyId);

                        -- Total Bilty Freight & Counts in FY
                        SELECT 
                            COUNT(*),
                            ISNULL(SUM(NetAmt), 0),
                            ISNULL(SUM(CASE WHEN PType = 'Paid' THEN NetAmt ELSE 0 END), 0),
                            ISNULL(SUM(CASE WHEN PType = 'ToPay' THEN NetAmt ELSE 0 END), 0)
                        FROM ISSHEAD 
                        WHERE IsDeleted = 0 
                          AND (@CompanyId = 0 OR CompanyId = @CompanyId)
                          AND (DocDate BETWEEN @SDate AND @EDate);

                        -- Challans Dispatched & Unique Vehicles
                        SELECT 
                            COUNT(*),
                            COUNT(DISTINCT VehicleNo)
                        FROM ChallanHead 
                        WHERE IsDeleted = 0 
                          AND (@CompanyId = 0 OR CompanyId = @CompanyId)
                          AND (DocDate BETWEEN @SDate AND @EDate);

                        -- Net Commission Book
                        SELECT ISNULL(SUM(TotNet), 0)
                        FROM CommHead 
                        WHERE IsDeleted = 0 
                          AND (@CompanyId = 0 OR CompanyId = @CompanyId)
                          AND (DocDate BETWEEN @SDate AND @EDate);
                    ";

                    using (SqlCommand cmd = new SqlCommand(sql, con))
                    {
                        cmd.CommandTimeout = 90;
                        cmd.Parameters.AddWithValue("@CompanyId", selectedCompanyId);
                        cmd.Parameters.AddWithValue("@SDate", fyStartDate);
                        cmd.Parameters.AddWithValue("@EDate", fyEndDate);

                        using (var reader = cmd.ExecuteReader())
                        {
                            // 1. Cash
                            if (reader.Read()) vm.CashBalance = reader.IsDBNull(0) ? 0 : reader.GetDecimal(0);
                            
                            // 2. Bank
                            reader.NextResult();
                            if (reader.Read()) vm.BankBalance = reader.IsDBNull(0) ? 0 : reader.GetDecimal(0);

                            // 3. Receivables
                            reader.NextResult();
                            if (reader.Read()) vm.TotalReceivables = reader.IsDBNull(0) ? 0 : reader.GetDecimal(0);

                            // 4. Payables
                            reader.NextResult();
                            if (reader.Read()) vm.TotalPayables = reader.IsDBNull(0) ? 0 : reader.GetDecimal(0);

                            // 5. Advances
                            reader.NextResult();
                            if (reader.Read()) vm.DriverAdvanceBalance = reader.IsDBNull(0) ? 0 : reader.GetDecimal(0);

                            // 6. ToPay Bilties
                            reader.NextResult();
                            if (reader.Read())
                            {
                                vm.PendingToPayCount = reader.GetInt32(0);
                                vm.PendingToPayAmount = reader.IsDBNull(1) ? 0 : reader.GetDecimal(1);
                            }

                            // 7. Bilty Freight
                            reader.NextResult();
                            if (reader.Read())
                            {
                                vm.TotalBiltiesCount = reader.GetInt32(0);
                                vm.TotalFreightBooked = reader.IsDBNull(1) ? 0 : reader.GetDecimal(1);
                                vm.PaidFreightTotal = reader.IsDBNull(2) ? 0 : reader.GetDecimal(2);
                                vm.ToPayFreightTotal = reader.IsDBNull(3) ? 0 : reader.GetDecimal(3);
                            }

                            // 8. Challans
                            reader.NextResult();
                            if (reader.Read())
                            {
                                vm.TotalChallansCount = reader.GetInt32(0);
                                vm.ActiveVehiclesCount = reader.GetInt32(1);
                            }

                            // 9. Commission
                            reader.NextResult();
                            if (reader.Read())
                            {
                                vm.TotalNetCommission = reader.IsDBNull(0) ? 0 : reader.GetDecimal(0);
                            }
                        }
                    }

                    // 4. Monthly Freight Booking Trend (Last 6-12 Months)
                    string monthlySql = @"
                        SELECT 
                            FORMAT(DocDate, 'MMM yy') as MonthLabel,
                            YEAR(DocDate) as Y,
                            MONTH(DocDate) as M,
                            COUNT(*) as TotalBilties,
                            SUM(ISNULL(NetAmt, 0)) as TotalFreight
                        FROM ISSHEAD
                        WHERE IsDeleted = 0 
                          AND (@CompanyId = 0 OR CompanyId = @CompanyId)
                          AND DocDate >= DATEADD(MONTH, -11, CAST(GETDATE() AS DATE))
                        GROUP BY FORMAT(DocDate, 'MMM yy'), YEAR(DocDate), MONTH(DocDate)
                        ORDER BY Y, M;
                    ";

                    using (SqlCommand cmdMonth = new SqlCommand(monthlySql, con))
                    {
                        cmdMonth.Parameters.AddWithValue("@CompanyId", selectedCompanyId);
                        using (var reader = cmdMonth.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                vm.MonthlyLabels.Add(reader["MonthLabel"].ToString() ?? "");
                                vm.MonthlyBilties.Add(Convert.ToInt32(reader["TotalBilties"]));
                                vm.MonthlyFreight.Add(Convert.ToDecimal(reader["TotalFreight"]));
                            }
                        }
                    }

                    // 5. Station-Wise Freight Distribution (Top 5 Stations)
                    string stationSql = @"
                        SELECT TOP 5
                            COALESCE(g.Name, h.Fooder, 'Station') AS StationName,
                            SUM(ISNULL(h.NetAmt, 0)) AS TotalFreight
                        FROM ISSHEAD h
                        LEFT JOIN GLCHART3 g ON g.Id = h.StationId
                        WHERE h.IsDeleted = 0 
                          AND (@CompanyId = 0 OR h.CompanyId = @CompanyId)
                          AND (h.DocDate BETWEEN @SDate AND @EDate)
                        GROUP BY COALESCE(g.Name, h.Fooder, 'Station')
                        ORDER BY TotalFreight DESC;
                    ";

                    using (SqlCommand cmdStation = new SqlCommand(stationSql, con))
                    {
                        cmdStation.Parameters.AddWithValue("@CompanyId", selectedCompanyId);
                        cmdStation.Parameters.AddWithValue("@SDate", fyStartDate);
                        cmdStation.Parameters.AddWithValue("@EDate", fyEndDate);
                        using (var reader = cmdStation.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                vm.StationNames.Add(reader["StationName"].ToString() ?? "");
                                vm.StationFreights.Add(Convert.ToDecimal(reader["TotalFreight"]));
                            }
                        }
                    }

                    // 6. Top Outstanding Parties (Debtors)
                    string topPartiesSql = @"
                        SELECT TOP 6
                            MAX(Name) AS Name,
                            SUM(ABS(ISNULL(Opening, 0))) AS Balance
                        FROM GLChart3
                        WHERE AC1 = '002' AND Opening <> 0
                          AND (@CompanyId = 0 OR CompanyId = @CompanyId)
                        GROUP BY RTRIM(AC1) + RTRIM(AC3)
                        ORDER BY Balance DESC;
                    ";

                    using (SqlCommand cmdParties = new SqlCommand(topPartiesSql, con))
                    {
                        cmdParties.Parameters.AddWithValue("@CompanyId", selectedCompanyId);
                        using (var reader = cmdParties.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                vm.TopPartyNames.Add(reader["Name"].ToString() ?? "");
                                vm.TopPartyBalances.Add(Convert.ToDecimal(reader["Balance"]));
                            }
                        }
                    }

                    // 7. Top Transporters / Creditors
                    string topTransSql = @"
                        SELECT TOP 6
                            MAX(Name) AS Name,
                            SUM(ABS(ISNULL(Opening, 0))) AS Balance
                        FROM GLChart3
                        WHERE AC1 = '052' AND Opening <> 0
                          AND (@CompanyId = 0 OR CompanyId = @CompanyId)
                        GROUP BY RTRIM(AC1) + RTRIM(AC3)
                        ORDER BY Balance DESC;
                    ";

                    using (SqlCommand cmdTrans = new SqlCommand(topTransSql, con))
                    {
                        cmdTrans.Parameters.AddWithValue("@CompanyId", selectedCompanyId);
                        using (var reader = cmdTrans.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                vm.TopTransporterNames.Add(reader["Name"].ToString() ?? "");
                                vm.TopTransporterBalances.Add(Convert.ToDecimal(reader["Balance"]));
                            }
                        }
                    }

                    // 8. Recent Bilties Feed (Top 6)
                    string recentBiltiesSql = @"
                        SELECT TOP 6
                            h.Id,
                            h.DocNo,
                            h.DocDate,
                            COALESCE(c.Name, h.CusName, 'Party') AS PartyName,
                            COALESCE(s.Name, h.Fooder, 'Station') AS StationName,
                            ISNULL(h.Qty, 0) AS Qty,
                            ISNULL(h.NetAmt, 0) AS Freight,
                            ISNULL(h.PType, 'Paid') AS PType
                        FROM ISSHEAD h
                        LEFT JOIN GLCHART3 c ON c.Id = h.CustomerId
                        LEFT JOIN GLCHART3 s ON s.Id = h.StationId
                        WHERE h.IsDeleted = 0 
                          AND (@CompanyId = 0 OR h.CompanyId = @CompanyId)
                        ORDER BY h.DocDate DESC, h.Id DESC;
                    ";

                    using (SqlCommand cmdRecentBilty = new SqlCommand(recentBiltiesSql, con))
                    {
                        cmdRecentBilty.Parameters.AddWithValue("@CompanyId", selectedCompanyId);
                        using (var reader = cmdRecentBilty.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                vm.RecentBilties.Add(new RecentBiltyItem
                                {
                                    Id = Convert.ToInt32(reader["Id"]),
                                    DocNo = reader["DocNo"].ToString() ?? "",
                                    DocDate = reader.IsDBNull(reader.GetOrdinal("DocDate")) ? DateTime.Now : Convert.ToDateTime(reader["DocDate"]),
                                    PartyName = reader["PartyName"].ToString() ?? "",
                                    StationName = reader["StationName"].ToString() ?? "",
                                    Qty = Convert.ToDecimal(reader["Qty"]),
                                    Freight = Convert.ToDecimal(reader["Freight"]),
                                    PType = reader["PType"].ToString() ?? "Paid"
                                });
                            }
                        }
                    }

                    // 9. Recent Financial Vouchers Feed (Top 6)
                    string recentVoSql = @"
                        SELECT TOP 6
                            v.Vono,
                            v.Vodate,
                            v.Votype,
                            ISNULL(v.Totdramt, 0) AS Dramt,
                            ISNULL(v.Totcramt, 0) AS Cramt,
                            ISNULL(v.Narration, '') AS Narration,
                            COALESCE(g.Name, v.PersonName, '') AS AccName
                        FROM VoHead v
                        LEFT JOIN GLCHART3 g ON g.Id = v.gl3Id
                        WHERE v.IsDeleted = 0 
                          AND (@CompanyId = 0 OR v.CompanyId = @CompanyId)
                        ORDER BY v.Vodate DESC, v.Id DESC;
                    ";

                    using (SqlCommand cmdVo = new SqlCommand(recentVoSql, con))
                    {
                        cmdVo.Parameters.AddWithValue("@CompanyId", selectedCompanyId);
                        using (var reader = cmdVo.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                vm.RecentTransactions.Add(new SimpleTransaction
                                {
                                    DocNo = reader["Vono"].ToString() ?? "",
                                    Date = reader.IsDBNull(reader.GetOrdinal("Vodate")) ? DateTime.Now : Convert.ToDateTime(reader["Vodate"]),
                                    VoType = reader["Votype"].ToString() ?? "JV",
                                    Debit = Convert.ToDecimal(reader["Dramt"]),
                                    Credit = Convert.ToDecimal(reader["Cramt"]),
                                    Narration = reader["Narration"].ToString() ?? "",
                                    Account = reader["AccName"].ToString() ?? ""
                                });
                            }
                        }
                    }

                    // 10. Recent Challans Feed (Top 5)
                    string recentChallanSql = @"
                        SELECT TOP 5
                            c.Id,
                            COALESCE(c.DocNo, CAST(c.ChalNo AS NVARCHAR), '') AS DocNo,
                            c.DocDate,
                            ISNULL(c.VehicleNo, '') AS VehicleNo,
                            ISNULL(c.Driver, '') AS Driver,
                            ISNULL(c.Transporter, '') AS Transporter,
                            ISNULL(c.Station, '') AS Station,
                            ISNULL(c.NetAmt, 0) AS NetAmt
                        FROM ChallanHead c
                        WHERE c.IsDeleted = 0 
                          AND (@CompanyId = 0 OR c.CompanyId = @CompanyId)
                        ORDER BY c.DocDate DESC, c.Id DESC;
                    ";

                    using (SqlCommand cmdChallan = new SqlCommand(recentChallanSql, con))
                    {
                        cmdChallan.Parameters.AddWithValue("@CompanyId", selectedCompanyId);
                        using (var reader = cmdChallan.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                vm.RecentChallans.Add(new RecentChallanItem
                                {
                                    Id = Convert.ToInt32(reader["Id"]),
                                    DocNo = reader["DocNo"].ToString() ?? "",
                                    DocDate = reader.IsDBNull(reader.GetOrdinal("DocDate")) ? DateTime.Now : Convert.ToDateTime(reader["DocDate"]),
                                    VehicleNo = reader["VehicleNo"].ToString() ?? "",
                                    Driver = reader["Driver"].ToString() ?? "",
                                    Transporter = reader["Transporter"].ToString() ?? "",
                                    Station = reader["Station"].ToString() ?? "",
                                    NetAmt = Convert.ToDecimal(reader["NetAmt"])
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = ex.Message;
            }

            return View(vm);
        }
    }
}
