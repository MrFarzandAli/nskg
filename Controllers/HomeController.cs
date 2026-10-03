using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Nskg.Data;
using Nskg.Extensions;
using Nskg.Helper;
using Nskg.Models;
using Nskg.Models.ViewModels;
using Nskg.Service.Interfaces;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Nskg.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _config;
        private readonly IPermissionService _permissionService;

        public HomeController(ApplicationDbContext context, IConfiguration config, IPermissionService permissionService)
        {
            _context = context;
            _config = config;
            _permissionService = permissionService;
        }

        public IActionResult Index(int? companyId, int? fyId)
        {
            var vm = new AdminDashboardViewModel();
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

            var financialYears = _context.FinancialYears
                .Where(f => !f.IsDeleted && !string.IsNullOrEmpty(f.YearName))
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

            int defaultCompanyId = User.GetCompanyId();
            int selectedCompanyId = companyId ?? (defaultCompanyId > 0 ? defaultCompanyId : 0);
            vm.SelectedCompanyId = selectedCompanyId;

            var selectedComp = companies.FirstOrDefault(c => c.Id == selectedCompanyId);
            vm.SelectedCompanyName = selectedComp != null ? selectedComp.Name : "All Companies / Branches";

            var defaultFy = financialYears.FirstOrDefault(f => !f.IsClosed) ?? financialYears.FirstOrDefault();
            int selectedFyId = fyId ?? (User.GetFinancialYearId() > 0 ? User.GetFinancialYearId() : (defaultFy?.Id ?? 4));
            vm.SelectedFyId = selectedFyId;

            var currentFyObj = financialYears.FirstOrDefault(f => f.Id == selectedFyId) ?? defaultFy;
            vm.SelectedFyName = currentFyObj?.YearName ?? "Current FY";
            DateTime fyStartDate = currentFyObj?.StartDate ?? new DateTime(DateTime.Now.Year, 7, 1);
            DateTime fyEndDate = currentFyObj?.EndDate ?? new DateTime(DateTime.Now.Year + 1, 6, 30);

            // 2. Fetch Operations Metrics via SQL
            try
            {
                using (SqlConnection con = new SqlConnection(connString))
                {
                    con.Open();

                    string sql = @"
                        -- Latest date with bookings for 'Today/Latest' activity
                        DECLARE @MaxDocDate DATE = (SELECT MAX(DocDate) FROM ISSHEAD WHERE IsDeleted = 0 AND (@CompanyId = 0 OR CompanyId = @CompanyId));
                        
                        -- Latest/Today's Booking
                        SELECT 
                            COUNT(*),
                            ISNULL(SUM(NetAmt), 0)
                        FROM ISSHEAD
                        WHERE IsDeleted = 0 
                          AND (@CompanyId = 0 OR CompanyId = @CompanyId)
                          AND DocDate = @MaxDocDate;

                        -- FY Total Bookings & Freight
                        SELECT 
                            COUNT(*),
                            ISNULL(SUM(NetAmt), 0),
                            ISNULL(SUM(CASE WHEN PType = 'Paid' THEN NetAmt ELSE 0 END), 0),
                            ISNULL(SUM(CASE WHEN PType = 'ToPay' THEN NetAmt ELSE 0 END), 0)
                        FROM ISSHEAD
                        WHERE IsDeleted = 0 
                          AND (@CompanyId = 0 OR CompanyId = @CompanyId)
                          AND (DocDate BETWEEN @SDate AND @EDate);

                        -- Challans & Fleet
                        SELECT 
                            COUNT(*),
                            COUNT(DISTINCT VehicleNo)
                        FROM ChallanHead
                        WHERE IsDeleted = 0 
                          AND (@CompanyId = 0 OR CompanyId = @CompanyId)
                          AND (DocDate BETWEEN @SDate AND @EDate);

                        -- Pending To-Pay Bilties
                        SELECT 
                            COUNT(*),
                            ISNULL(SUM(NetAmt), 0)
                        FROM ISSHEAD
                        WHERE PType = 'ToPay' AND IsDeleted = 0 
                          AND (@CompanyId = 0 OR CompanyId = @CompanyId);
                    ";

                    using (SqlCommand cmd = new SqlCommand(sql, con))
                    {
                        cmd.CommandTimeout = 90;
                        cmd.Parameters.AddWithValue("@CompanyId", selectedCompanyId);
                        cmd.Parameters.AddWithValue("@SDate", fyStartDate);
                        cmd.Parameters.AddWithValue("@EDate", fyEndDate);

                        using (var reader = cmd.ExecuteReader())
                        {
                            // 1. Today/Latest Day
                            if (reader.Read())
                            {
                                vm.TodayBiltiesCount = reader.GetInt32(0);
                                vm.TodayFreightBooked = reader.IsDBNull(1) ? 0 : reader.GetDecimal(1);
                            }

                            // 2. FY Totals
                            reader.NextResult();
                            if (reader.Read())
                            {
                                vm.MonthBiltiesCount = reader.GetInt32(0);
                                vm.MonthFreightBooked = reader.IsDBNull(1) ? 0 : reader.GetDecimal(1);
                                vm.PaidFreightAmount = reader.IsDBNull(2) ? 0 : reader.GetDecimal(2);
                                vm.ToPayFreightAmount = reader.IsDBNull(3) ? 0 : reader.GetDecimal(3);
                            }

                            // 3. Challans & Fleet
                            reader.NextResult();
                            if (reader.Read())
                            {
                                vm.TotalChallansCount = reader.GetInt32(0);
                                vm.ActiveVehiclesCount = reader.GetInt32(1);
                            }

                            // 4. Pending To-Pay
                            reader.NextResult();
                            if (reader.Read())
                            {
                                vm.PendingToPayCount = reader.GetInt32(0);
                                vm.PendingToPayAmount = reader.IsDBNull(1) ? 0 : reader.GetDecimal(1);
                            }
                        }
                    }

                    // 3. Monthly Bilty Volume Trend (Last 12 Months)
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

                    // 4. Top Stations Cargo Share
                    string stationSql = @"
                        SELECT TOP 6
                            COALESCE(g.Name, h.Fooder, 'Station') AS StationName,
                            COUNT(*) AS BiltyCount,
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
                                vm.StationBiltyCounts.Add(Convert.ToInt32(reader["BiltyCount"]));
                                vm.StationFreights.Add(Convert.ToDecimal(reader["TotalFreight"]));
                            }
                        }
                    }

                    // 5. Top Booking Parties
                    string partiesSql = @"
                        SELECT TOP 6
                            COALESCE(c.Name, h.CusName, 'Party') AS PartyName,
                            SUM(ISNULL(h.NetAmt, 0)) AS TotalFreight
                        FROM ISSHEAD h
                        LEFT JOIN GLCHART3 c ON c.Id = h.CustomerId
                        WHERE h.IsDeleted = 0 
                          AND (@CompanyId = 0 OR h.CompanyId = @CompanyId)
                          AND (h.DocDate BETWEEN @SDate AND @EDate)
                        GROUP BY COALESCE(c.Name, h.CusName, 'Party')
                        ORDER BY TotalFreight DESC;
                    ";

                    using (SqlCommand cmdParties = new SqlCommand(partiesSql, con))
                    {
                        cmdParties.Parameters.AddWithValue("@CompanyId", selectedCompanyId);
                        cmdParties.Parameters.AddWithValue("@SDate", fyStartDate);
                        cmdParties.Parameters.AddWithValue("@EDate", fyEndDate);
                        using (var reader = cmdParties.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                vm.TopBookingParties.Add(reader["PartyName"].ToString() ?? "");
                                vm.TopBookingFreights.Add(Convert.ToDecimal(reader["TotalFreight"]));
                            }
                        }
                    }

                    // 6. Recent Bilties Feed
                    string recentBiltiesSql = @"
                        SELECT TOP 8
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

                    // 7. Recent Dispatched Challans
                    string recentChallanSql = @"
                        SELECT TOP 8
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

        public async Task<IActionResult> Privacy()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var hasAccess = await _permissionService
                .HasPermissionAsync(userId, "Home", "Privacy", Permissions.View);

            if (!hasAccess)
                return RedirectToAction("AccessDenied");

            return View();
        }

        public IActionResult AccessDenied()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
