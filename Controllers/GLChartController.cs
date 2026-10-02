
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nskg.Data;
using Nskg.Extensions;
using Nskg.Helper;
using Nskg.Models;
using Nskg.Models.ViewModels;
using Nskg.Repositories.Interfaces;
using System.Security.Principal;

namespace Nskg.Controllers
{
    public class GLChartController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditService _audit;


        public GLChartController(ApplicationDbContext context, IAuditService audit)
        {
            _context = context;
            _audit = audit;
        }

        public IActionResult Index()
        {           
            int companyId = User.GetCompanyId();
            string companyCode = User.GetCompanyCode();

            return View(new ChartVM
            {
                Accounts = _context.GLChart1
                    .Where(x => x.CompanyId == companyId || x.CoCode == companyCode)
                    .ToList(),
                Categories = _context.AccCat
                    .Where(x => !x.IsDeleted && (x.CompanyId == companyId || x.CoCode == companyCode))
                    .Select(x => new SelectListItem
                    {
                        Value = x.CatCode,     // or x.Id if you have Id
                        Text = x.Category
                    })
                    .ToList()
            });
        }

        public IActionResult GetDetails(int accountId)
        {
            int companyId = User.GetCompanyId();
            string companyCode = User.GetCompanyCode();

            var data = _context.GLChart3
                .Where(x => x.GLChart1Id == accountId && (x.CompanyId == companyId || x.CoCode == companyCode))
                .ToList();

            return PartialView("_GLChart3List", data);
        }

       
        [HttpPost]
        public async Task<IActionResult> AddAccount(GLChart1 model, bool createInAllCompanies = false)
        {
            try
            {
                int currentCompanyId = User.GetCompanyId();
                string currentCompanyCode = User.GetCompanyCode();

                string ac1Code = GenerateAC1();
                model.AC1 = ac1Code;
                model.CompanyId = currentCompanyId;
                model.CoCode = currentCompanyCode;

                _context.GLChart1.Add(model);
                _context.SaveChanges();

                await _audit.LogAsync("Create", "GLChart1", model.Id.ToString(), $"Created Account: {model.Name}");

                if (createInAllCompanies)
                {
                    var otherCompanies = _context.Companies
                        .Where(c => !c.IsDeleted && c.Id != currentCompanyId)
                        .ToList();

                    foreach (var comp in otherCompanies)
                    {
                        var exists = _context.GLChart1.Any(x => (x.CompanyId == comp.Id || x.CoCode == comp.Cocode) && (x.AC1 == ac1Code || x.Name == model.Name));
                        if (!exists)
                        {
                            var clone = new GLChart1
                            {
                                AC1 = ac1Code,
                                Name = model.Name,
                                AcType = model.AcType,
                                CType = model.CType,
                                IncBal = model.IncBal,
                                CompanyId = comp.Id,
                                CoCode = comp.Cocode,
                                Opening = 0
                            };
                            _context.GLChart1.Add(clone);
                        }
                    }
                    _context.SaveChanges();
                }

                TempData["SuccessMessage"] = createInAllCompanies ? "Account created in all companies successfully!" : "Account created successfully!";
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                await _audit.LogAsync("Error", "GLChart1", "0", ex.Message);
                return Json(new { success = false, message = ex.Message });
            }
        }       

        [HttpPost]
        public async Task<IActionResult> UpdateAccount(GLChart1 model)
        {
            try
            {
                var data = _context.GLChart1.Find(model.Id);
                if (data == null)
                    return Json(new { success = false, message = "Not found!" });

                data.Name = model.Name;
                data.CType = model.CType;
                data.AcType = model.AcType;
                data.CompanyId = User.GetCompanyId();
                data.CoCode = User.GetCompanyCode();

                _context.SaveChanges();

                await _audit.LogAsync("Update", "GLChart1", model.Id.ToString(), $"Updated Account: {model.Name}");

                TempData["SuccessMessage"] = "Account updated successfully!";
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                await _audit.LogAsync("Error", "GLChart1", model.Id.ToString(), ex.Message);
                return Json(new { success = false, message = ex.Message });
            }
        }

       
        [HttpDelete]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAccount(int id)
        {
            try
            {
                var data = _context.GLChart1.Find(id);
                if (data == null)
                    return Json(new { success = false, message = "Not found!" });

                // Check for existing details
                var hasDetails = _context.GLChart3.Any(x => x.GLChart1Id == id);
                if (hasDetails)
                {
                    return Json(new { success = false, message = "Cannot delete account with existing details. Please delete all details first." });
                }

                _context.GLChart1.Remove(data);
                _context.SaveChanges();

                await _audit.LogAsync("Delete", "GLChart1", id.ToString(), $"Deleted Account: {data.Name}");

                TempData["SuccessMessage"] = "Account deleted successfully!";
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                await _audit.LogAsync("Error", "GLChart1", id.ToString(), ex.Message);
                return Json(new { success = false, message = ex.Message });
            }
        }

        // DETAIL
       
        [HttpPost]
        public async Task<IActionResult> AddDetail(GLChart3 model, bool createInAllCompanies = false)
        {
            try
            {
                int currentCompanyId = User.GetCompanyId();
                string currentCompanyCode = User.GetCompanyCode();

                var account = _context.GLChart1.Find(model.GLChart1Id);
                if (account == null)
                    return Json(new { success = false, message = "Parent account not found!" });

                string ac3Code = GenerateAC3(model.GLChart1Id);

                model.AC1 = account.AC1;
                model.AC3 = ac3Code;
                model.ACC = account.AC1 + ac3Code;
                model.AcType = account.AcType;
                model.CType = account.CType;
                model.CHName = account.Name;
                Guid? newGroupId = null;
                if (createInAllCompanies)
                {
                    newGroupId = Guid.NewGuid();
                    model.LinkedGroupId = newGroupId;
                }

                model.CoCode = currentCompanyCode;
                model.CompanyId = currentCompanyId;

                _context.GLChart3.Add(model);
                _context.SaveChanges();

                await _audit.LogAsync("Create", "GLChart3", model.Id.ToString(), $"Created Detail: {model.Name}");

                if (createInAllCompanies)
                {
                    var otherCompanies = _context.Companies
                        .Where(c => !c.IsDeleted && c.Id != currentCompanyId)
                        .ToList();

                    foreach (var comp in otherCompanies)
                    {
                        var otherParent = _context.GLChart1.FirstOrDefault(x =>
                            (x.CompanyId == comp.Id || x.CoCode == comp.Cocode) &&
                            (x.AC1 == account.AC1 || x.Name == account.Name));

                        if (otherParent == null)
                        {
                            otherParent = new GLChart1
                            {
                                AC1 = account.AC1,
                                Name = account.Name,
                                AcType = account.AcType,
                                CType = account.CType,
                                IncBal = account.IncBal,
                                CompanyId = comp.Id,
                                CoCode = comp.Cocode,
                                Opening = 0
                            };
                            _context.GLChart1.Add(otherParent);
                            _context.SaveChanges();
                        }

                        var existingAccount = _context.GLChart3.FirstOrDefault(x =>
                            (x.CompanyId == comp.Id || x.CoCode == comp.Cocode) &&
                            x.GLChart1Id == otherParent.Id &&
                            (x.AC3 == ac3Code || x.Name == model.Name));

                        if (existingAccount != null)
                        {
                            existingAccount.LinkedGroupId = newGroupId;
                        }
                        else
                        {
                            string compAc3 = GenerateAC3(otherParent.Id);
                            var cloneDetail = new GLChart3
                            {
                                GLChart1Id = otherParent.Id,
                                AC1 = otherParent.AC1,
                                AC3 = compAc3,
                                ACC = otherParent.AC1 + compAc3,
                                Name = model.Name,
                                AcType = otherParent.AcType,
                                CType = otherParent.CType,
                                CHName = otherParent.Name,
                                CoCode = comp.Cocode,
                                CompanyId = comp.Id,
                                Opening = 0,
                                LinkedGroupId = newGroupId
                            };
                            _context.GLChart3.Add(cloneDetail);
                        }
                    }
                    _context.SaveChanges();
                }

                return Json(new { success = true, message = createInAllCompanies ? "Detail added in all companies successfully!" : "Detail added successfully!" });
            }
            catch (Exception ex)
            {
                await _audit.LogAsync("Error", "GLChart3", "0", ex.Message);
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public IActionResult CreateAjax(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return Json(new { success = false, message = "Name required" });
            }

            var customeracountId = _context.AcPara
                   .Where(a => AccountCategories.Customer.Contains(a.ActypeCode)
                               && a.Cocode == User.GetCompanyId().ToString()
                               && a.Parent == "P").Select(x => x.Id).Max();

            var glchart1 = _context.GLChart1.Find(customeracountId);

            var customer = new GLChart3
            {
                GLChart1Id = glchart1.Id,
                AC1 = glchart1.AC1,
                AC3 = GenerateAC3(glchart1.Id),
                ACC = glchart1.AC1 + GenerateAC3(glchart1.Id),
                AcType = glchart1.AcType,
                CType = glchart1.CType,
                CHName = glchart1.Name,
                CoCode = User.GetCompanyCode(),
                CompanyId = User.GetCompanyId(),
                Name = name
            };

            _context.GLChart3.Add(customer);
            _context.SaveChanges();

            return Json(new
            {
                success = true,
                id = customer.Id,
                text = customer.Name
            });
        }

        [HttpGet]
        public IActionResult GetDetail(int id)
        {
            var data = _context.GLChart3.Find(id);

            return Json(new
            {
                id = data.Id,
                name = data.Name,
                glChart1Id = data.GLChart1Id
            });
        }

        [HttpPost]
        public async Task<IActionResult> UpdateDetail(GLChart3 model)
        {
            try
            {
                var data = _context.GLChart3.Find(model.Id);
                if (data == null)
                    return Json(new { success = false, message = "Not found!" });

                data.Name = model.Name;
                _context.SaveChanges();

                await _audit.LogAsync("Update", "GLChart3", model.Id.ToString(), $"Updated Detail: {model.Name}");

                return Json(new { success = true, message = "Detail updated!" });
            }
            catch (Exception ex)
            {
                await _audit.LogAsync("Error", "GLChart3", model.Id.ToString(), ex.Message);
                return Json(new { success = false, message = ex.Message });
            }
        }


      
        [HttpDelete]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteDetail(int id)
        {
            try
            {
                var data = _context.GLChart3.Find(id);
                if (data == null)
                    return Json(new { success = false, message = "Not found!" });

                _context.GLChart3.Remove(data);
                _context.SaveChanges();

                await _audit.LogAsync("Delete", "GLChart3", id.ToString(), $"Deleted Detail: {data.Name}");

                return Json(new { success = true, message = "Deleted successfully!" });
            }
            catch (Exception ex)
            {
                await _audit.LogAsync("Error", "GLChart3", id.ToString(), ex.Message);
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public IActionResult GetAccountLinkingData(int detailId)
        {
            var source = _context.GLChart3.Find(detailId);
            if (source == null)
                return Json(new { success = false, message = "Account not found!" });

            var currentCompany = _context.Companies.FirstOrDefault(c => c.Id == source.CompanyId || c.Cocode == source.CoCode);
            string currentCompanyName = currentCompany != null ? ((!string.IsNullOrEmpty(currentCompany.Cocode) ? currentCompany.Cocode + " - " : "") + currentCompany.Name) : "Current Company";

            var otherCompanies = _context.Companies
                .Where(c => !c.IsDeleted && c.Id != source.CompanyId && c.Cocode != source.CoCode)
                .OrderBy(c => c.Cocode)
                .Select(c => new
                {
                    companyId = c.Id,
                    coCode = c.Cocode,
                    companyName = (!string.IsNullOrEmpty(c.Cocode) ? c.Cocode + " - " : "") + c.Name
                })
                .ToList();

            var linkedAccountIds = new HashSet<int>();
            if (source.LinkedGroupId.HasValue)
            {
                linkedAccountIds = _context.GLChart3
                    .Where(x => x.LinkedGroupId == source.LinkedGroupId.Value && x.Id != detailId)
                    .Select(x => x.Id)
                    .ToHashSet();
            }

            var companiesData = new List<object>();
            foreach (var comp in otherCompanies)
            {
                // Prioritize accounts under the same AC1 head
                var accounts = _context.GLChart3
                    .Where(x => (x.CompanyId == comp.companyId || x.CoCode == comp.coCode) && x.AC1 == source.AC1)
                    .OrderBy(x => x.ACC)
                    .Select(x => new
                    {
                        id = x.Id,
                        code = x.ACC ?? (x.AC1 + x.AC3),
                        name = x.Name,
                        isLinked = linkedAccountIds.Contains(x.Id)
                    })
                    .ToList();

                if (!accounts.Any())
                {
                    accounts = _context.GLChart3
                        .Where(x => (x.CompanyId == comp.companyId || x.CoCode == comp.coCode))
                        .OrderBy(x => x.Name)
                        .Select(x => new
                        {
                            id = x.Id,
                            code = x.ACC ?? (x.AC1 + x.AC3),
                            name = x.Name,
                            isLinked = linkedAccountIds.Contains(x.Id)
                        })
                        .Take(500)
                        .ToList();
                }

                var currentlyLinkedAcc = accounts.FirstOrDefault(a => a.isLinked);

                companiesData.Add(new
                {
                    companyId = comp.companyId,
                    coCode = comp.coCode,
                    companyName = comp.companyName,
                    accounts = accounts,
                    selectedAccountId = currentlyLinkedAcc?.id ?? 0,
                    isLinked = currentlyLinkedAcc != null
                });
            }

            return Json(new
            {
                success = true,
                source = new
                {
                    id = source.Id,
                    code = source.ACC ?? (source.AC1 + source.AC3),
                    name = source.Name,
                    companyName = currentCompanyName,
                    linkedGroupId = source.LinkedGroupId?.ToString()
                },
                companies = companiesData
            });
        }

        [HttpPost]
        public async Task<IActionResult> SaveAccountLinks([FromBody] SaveAccountLinksViewModel model)
        {
            try
            {
                if (model == null || model.SourceDetailId == 0)
                    return Json(new { success = false, message = "Invalid request payload!" });

                var source = _context.GLChart3.Find(model.SourceDetailId);
                if (source == null)
                    return Json(new { success = false, message = "Source account not found!" });

                Guid groupId = source.LinkedGroupId ?? Guid.NewGuid();
                source.LinkedGroupId = groupId;

                var currentlyLinked = _context.GLChart3
                    .Where(x => x.LinkedGroupId == groupId && x.Id != source.Id)
                    .ToList();

                var targetSet = new HashSet<int>(model.TargetDetailIds ?? new List<int>());

                if (targetSet.Count == 0)
                {
                    source.LinkedGroupId = null;
                    foreach (var item in currentlyLinked)
                    {
                        item.LinkedGroupId = null;
                    }
                }
                else
                {
                    // Unlink deselected accounts
                    foreach (var item in currentlyLinked)
                    {
                        if (!targetSet.Contains(item.Id))
                        {
                            item.LinkedGroupId = null;
                        }
                    }

                    // Link selected accounts
                    foreach (var targetId in targetSet)
                    {
                        var target = _context.GLChart3.Find(targetId);
                        if (target != null)
                        {
                            target.LinkedGroupId = groupId;
                        }
                    }
                }

                await _context.SaveChangesAsync();
                await _audit.LogAsync("Link", "GLChart3", source.Id.ToString(), $"Linked account {source.Name} across companies");

                return Json(new { success = true, message = "Accounts successfully linked across selected companies!" });
            }
            catch (Exception ex)
            {
                await _audit.LogAsync("Error", "GLChart3", model?.SourceDetailId.ToString() ?? "0", ex.Message);
                return Json(new { success = false, message = ex.Message });
            }
        }
        private string GenerateAC1()
        {
            int companyId = User.GetCompanyId();
            string companyCode = User.GetCompanyCode();

            var last = _context.GLChart1
                .Where(x => x.CompanyId == companyId || x.CoCode == companyCode)
                .OrderByDescending(x => x.Id)
                .FirstOrDefault();

            if (last == null || string.IsNullOrEmpty(last.AC1))
                return "01";

            if (int.TryParse(last.AC1, out int current))
            {
                return (current + 1).ToString("D2");
            }
            return "01";
        }

        private string GenerateAC3(int accountId)
        {
            var last = _context.GLChart3
                .Where(x => x.GLChart1Id == accountId)
                .OrderByDescending(x => x.Id)
                .FirstOrDefault();

            if (last == null)
                return "001";

            int next = int.Parse(last.AC3) + 1;
            return next.ToString("D3");
        }
    }
}
