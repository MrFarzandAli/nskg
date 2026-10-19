
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
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
                    .OrderBy(x => x.AC1)
                    .ToList(),
                Categories = _context.AccCat
                    .Where(x => !x.IsDeleted && (x.CompanyId == companyId || x.CoCode == companyCode))
                    .Select(x => new SelectListItem
                    {
                        Value = x.CatCode,     // or x.Id if you have Id
                        Text = x.Category
                    })
                    .ToList(),
                Companies = _context.Companies
                    .Where(c => !c.IsDeleted && c.Id != companyId && c.Cocode != companyCode)
                    .OrderBy(c => c.Cocode)
                    .ToList()
            });
        }

        public IActionResult GetDetails(int accountId)
        {
            int companyId = User.GetCompanyId();
            string companyCode = User.GetCompanyCode();

            var data = _context.GLChart3
                .Where(x => x.GLChart1Id == accountId && (x.CompanyId == companyId || x.CoCode == companyCode))
                .OrderBy(x => x.AC1)
                .ThenBy(x => x.AC3)
                .ToList();

            return PartialView("_GLChart3List", data);
        }

       
        [HttpPost]
        public async Task<IActionResult> AddAccount(GLChart1 model, List<int>? targetCompanyIds = null, bool createInOtherCompanies = false, bool createInAllCompanies = false)
        {
            try
            {
                int currentCompanyId = User.GetCompanyId();
                string currentCompanyCode = User.GetCompanyCode();

                string trimmedName = model.Name?.Trim() ?? "";
                if (string.IsNullOrEmpty(trimmedName))
                    return Json(new { success = false, message = "Account name is required!" });

                var existsInCurrent = _context.GLChart1.Any(x =>
                    (x.CompanyId == currentCompanyId || x.CoCode == currentCompanyCode) &&
                    x.Name.ToLower() == trimmedName.ToLower());

                if (existsInCurrent)
                {
                    return Json(new { success = false, message = $"Account '{trimmedName}' already exists!" });
                }

                bool shouldCreateInOther = (createInOtherCompanies || createInAllCompanies);
                List<Company> selectedCompanies = new();
                if (shouldCreateInOther)
                {
                    if (targetCompanyIds != null && targetCompanyIds.Any())
                    {
                        selectedCompanies = _context.Companies
                            .Where(c => !c.IsDeleted && targetCompanyIds.Contains(c.Id) && c.Id != currentCompanyId && c.Cocode != currentCompanyCode)
                            .ToList();
                    }
                    else if (createInAllCompanies)
                    {
                        selectedCompanies = _context.Companies
                            .Where(c => !c.IsDeleted && c.Id != currentCompanyId && c.Cocode != currentCompanyCode)
                            .ToList();
                    }
                    shouldCreateInOther = selectedCompanies.Any();
                }

                string ac1Code = GenerateAC1(shouldCreateInOther);
                model.AC1 = ac1Code;
                model.Name = trimmedName;
                model.CompanyId = currentCompanyId;
                model.CoCode = currentCompanyCode;

                _context.GLChart1.Add(model);
                _context.SaveChanges();

                await _audit.LogAsync("Create", "GLChart1", model.Id.ToString(), $"Created Account: {model.Name}");

                if (shouldCreateInOther)
                {
                    foreach (var comp in selectedCompanies)
                    {
                        var exists = _context.GLChart1.Any(x => (x.CompanyId == comp.Id || x.CoCode == comp.Cocode) && (x.AC1 == ac1Code || x.Name.ToLower() == trimmedName.ToLower()));
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

                string msg = shouldCreateInOther ? $"Account created in {selectedCompanies.Count + 1} companies successfully!" : "Account created successfully!";
                TempData["SuccessMessage"] = msg;
                return Json(new { success = true, message = msg });
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
       
        [HttpGet]
        public IActionResult GetNextDetailCode(int accountId, bool createInAllCompanies = false, bool createInOtherCompanies = false, string? targetCompanyIds = null)
        {
            var account = _context.GLChart1.Find(accountId);
            if (account == null)
                return Json(new { success = false, message = "Account not found" });

            int currentCompanyId = User.GetCompanyId();
            string currentCompanyCode = User.GetCompanyCode();

            bool hasTargets = createInAllCompanies;
            if (createInOtherCompanies && !string.IsNullOrEmpty(targetCompanyIds))
            {
                var ids = targetCompanyIds.Split(',', StringSplitOptions.RemoveEmptyEntries);
                hasTargets = ids.Length > 0;
            }

            string nextAc3 = hasTargets
                ? GenerateAC3AcrossAllCompanies(account.AC1 ?? "")
                : GenerateAC3ForCompany(account.AC1 ?? "", currentCompanyId, currentCompanyCode);

            string ac1 = (account.AC1 ?? "").Trim();
            return Json(new
            {
                success = true,
                ac1 = ac1,
                ac3 = nextAc3,
                acc = ac1 + nextAc3,
                fullCode = $"{ac1} - {nextAc3}"
            });
        }

        [HttpPost]
        public async Task<IActionResult> AddDetail(GLChart3 model, List<int>? targetCompanyIds = null, bool createInOtherCompanies = false, bool createInAllCompanies = false)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                int currentCompanyId = User.GetCompanyId();
                string currentCompanyCode = User.GetCompanyCode();

                var account = await _context.GLChart1.FindAsync(model.GLChart1Id);
                if (account == null)
                    return Json(new { success = false, message = "Parent account not found!" });

                string trimmedName = model.Name?.Trim() ?? "";
                if (string.IsNullOrEmpty(trimmedName))
                    return Json(new { success = false, message = "Detail name is required!" });

                // Check if detail with this name already exists in current company under this account
                var existsInCurrent = await _context.GLChart3.AnyAsync(x =>
                    (x.CompanyId == currentCompanyId || x.CoCode == currentCompanyCode) &&
                    x.AC1 == account.AC1 &&
                    x.Name.ToLower() == trimmedName.ToLower());

                if (existsInCurrent)
                {
                    return Json(new { success = false, message = $"Detail '{trimmedName}' already exists under this account!" });
                }

                bool shouldCreateInOther = (createInOtherCompanies || createInAllCompanies);
                List<Company> selectedCompanies = new();

                if (shouldCreateInOther)
                {
                    if (targetCompanyIds != null && targetCompanyIds.Any())
                    {
                        selectedCompanies = await _context.Companies
                            .Where(c => !c.IsDeleted && targetCompanyIds.Contains(c.Id) && c.Id != currentCompanyId && c.Cocode != currentCompanyCode)
                            .ToListAsync();
                    }
                    else if (createInAllCompanies)
                    {
                        selectedCompanies = await _context.Companies
                            .Where(c => !c.IsDeleted && c.Id != currentCompanyId && c.Cocode != currentCompanyCode)
                            .ToListAsync();
                    }

                    shouldCreateInOther = selectedCompanies.Any();
                }

                string userAc3 = model.AC3?.Trim() ?? "";
                string ac3Code;
                Guid? newGroupId = null;

                if (shouldCreateInOther)
                {
                    newGroupId = Guid.NewGuid();

                    // Check if any selected company already has an account with this name under this AC1
                    foreach (var comp in selectedCompanies)
                    {
                        var nameExistsInComp = await _context.GLChart3.AnyAsync(x =>
                            (x.CompanyId == comp.Id || x.CoCode == comp.Cocode) &&
                            x.AC1 == account.AC1 &&
                            x.Name.ToLower() == trimmedName.ToLower());

                        if (nameExistsInComp)
                        {
                            return Json(new { success = false, message = $"Detail '{trimmedName}' already exists in company '{comp.Name}'!" });
                        }
                    }

                    if (!string.IsNullOrEmpty(userAc3))
                    {
                        ac3Code = int.TryParse(userAc3, out int num) ? num.ToString("D3") : userAc3;
                    }
                    else
                    {
                        ac3Code = GenerateAC3AcrossAllCompanies(account.AC1 ?? "");
                    }

                    // Strictly check that AC1 + AC3 does not already exist in current company
                    bool dupInCurrent = await _context.GLChart3.AnyAsync(x =>
                        (x.CompanyId == currentCompanyId || x.CoCode == currentCompanyCode) &&
                        x.AC1 == account.AC1 &&
                        x.AC3 == ac3Code);

                    if (dupInCurrent)
                    {
                        await transaction.RollbackAsync();
                        return Json(new { success = false, message = $"Account code {account.AC1}-{ac3Code} already exists in current company!" });
                    }

                    model.AC1 = account.AC1;
                    model.AC3 = ac3Code;
                    model.ACC = (account.AC1 ?? "") + ac3Code;
                    model.Name = trimmedName;
                    model.AcType = account.AcType;
                    model.CType = account.CType;
                    model.CHName = account.Name;
                    model.LinkedGroupId = newGroupId;
                    model.CoCode = currentCompanyCode;
                    model.CompanyId = currentCompanyId;
                    model.Opening = 0;

                    _context.GLChart3.Add(model);
                    await _context.SaveChangesAsync();

                    await _audit.LogAsync("Create", "GLChart3", model.Id.ToString(), $"Created Detail: {model.Name} ({model.ACC})");

                    foreach (var comp in selectedCompanies)
                    {
                        var otherParent = await _context.GLChart1.FirstOrDefaultAsync(x =>
                            (x.CompanyId == comp.Id || x.CoCode == comp.Cocode) &&
                            x.AC1 == account.AC1);

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
                            await _context.SaveChangesAsync();
                        }

                        // Strictly ensure same AC1||AC3 in one company is not allowed or created
                        bool duplicateInOtherComp = await _context.GLChart3.AnyAsync(x =>
                            (x.CompanyId == comp.Id || x.CoCode == comp.Cocode) &&
                            x.AC1 == otherParent.AC1 &&
                            x.AC3 == ac3Code);

                        if (duplicateInOtherComp)
                        {
                            await transaction.RollbackAsync();
                            return Json(new { success = false, message = $"Account code {otherParent.AC1}-{ac3Code} already exists in company '{comp.Name}'!" });
                        }

                        var cloneDetail = new GLChart3
                        {
                            GLChart1Id = otherParent.Id,
                            AC1 = otherParent.AC1,
                            AC3 = ac3Code, // EXACT SAME AC3 across selected companies!
                            ACC = (otherParent.AC1 ?? "") + ac3Code,
                            Name = trimmedName,
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

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    string compSummary = $"{selectedCompanies.Count + 1} companies";
                    return Json(new { success = true, message = $"Detail '{trimmedName}' (Code: {account.AC1}-{ac3Code}) created and linked in {compSummary} successfully!" });
                }
                else
                {
                    if (!string.IsNullOrEmpty(userAc3))
                    {
                        ac3Code = int.TryParse(userAc3, out int num) ? num.ToString("D3") : userAc3;
                    }
                    else
                    {
                        // Single company: Generate next MAX AC3 for this company under this AC1
                        ac3Code = GenerateAC3ForCompany(account.AC1 ?? "", currentCompanyId, currentCompanyCode);
                    }

                    // Double check that AC1 + AC3 does not already exist in current company
                    bool dupInCurrent = await _context.GLChart3.AnyAsync(x =>
                        (x.CompanyId == currentCompanyId || x.CoCode == currentCompanyCode) &&
                        x.AC1 == account.AC1 &&
                        x.AC3 == ac3Code);

                    if (dupInCurrent)
                    {
                        await transaction.RollbackAsync();
                        return Json(new { success = false, message = $"Account code {account.AC1}-{ac3Code} already exists in current company!" });
                    }

                    model.AC1 = account.AC1;
                    model.AC3 = ac3Code;
                    model.ACC = (account.AC1 ?? "") + ac3Code;
                    model.Name = trimmedName;
                    model.AcType = account.AcType;
                    model.CType = account.CType;
                    model.CHName = account.Name;
                    model.LinkedGroupId = null;
                    model.CoCode = currentCompanyCode;
                    model.CompanyId = currentCompanyId;
                    model.Opening = 0;

                    _context.GLChart3.Add(model);
                    await _context.SaveChangesAsync();

                    await _audit.LogAsync("Create", "GLChart3", model.Id.ToString(), $"Created Detail: {model.Name} ({model.ACC})");
                    await transaction.CommitAsync();

                    return Json(new { success = true, message = $"Detail '{trimmedName}' (Code: {account.AC1}-{ac3Code}) added successfully!" });
                }
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
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
            if (glchart1 == null)
            {
                return Json(new { success = false, message = "Customer head account not configured in AcPara" });
            }

            int currentCompanyId = User.GetCompanyId();
            string currentCompanyCode = User.GetCompanyCode();

            string newAc3 = GenerateAC3ForCompany(glchart1.AC1 ?? "", currentCompanyId, currentCompanyCode);

            bool dupExists = _context.GLChart3.Any(x =>
                (x.CompanyId == currentCompanyId || x.CoCode == currentCompanyCode) &&
                x.AC1 == glchart1.AC1 &&
                x.AC3 == newAc3);

            if (dupExists)
            {
                return Json(new { success = false, message = $"Account code {glchart1.AC1}-{newAc3} already exists in current company!" });
            }

            var customer = new GLChart3
            {
                GLChart1Id = glchart1.Id,
                AC1 = glchart1.AC1,
                AC3 = newAc3,
                ACC = (glchart1.AC1 ?? "") + newAc3,
                AcType = glchart1.AcType,
                CType = glchart1.CType,
                CHName = glchart1.Name,
                CoCode = currentCompanyCode,
                CompanyId = currentCompanyId,
                Name = name.Trim(),
                Opening = 0
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
            if (data == null)
                return Json(new { success = false, message = "Not found" });

            string ac1 = (data.AC1 ?? "").Trim();
            string ac3 = (data.AC3 ?? "").Trim();
            string acc = (data.ACC ?? "").Trim();
            if (string.IsNullOrEmpty(acc) && !string.IsNullOrEmpty(ac1) && !string.IsNullOrEmpty(ac3))
            {
                acc = ac1 + ac3;
            }

            return Json(new
            {
                success = true,
                id = data.Id,
                name = (data.Name ?? "").Trim(),
                glChart1Id = data.GLChart1Id,
                ac1 = ac1,
                ac3 = ac3,
                acc = acc,
                code = $"{ac1} - {ac3}"
            });
        }

        [HttpPost]
        public async Task<IActionResult> UpdateDetail(GLChart3 model)
        {
            try
            {
                var data = await _context.GLChart3.FindAsync(model.Id);
                if (data == null)
                    return Json(new { success = false, message = "Detail account not found!" });

                string trimmedName = model.Name?.Trim() ?? "";
                if (string.IsNullOrEmpty(trimmedName))
                    return Json(new { success = false, message = "Detail name is required!" });

                string inputAc3 = model.AC3?.Trim() ?? "";
                if (string.IsNullOrEmpty(inputAc3))
                    return Json(new { success = false, message = "Account code (AC3) is required!" });

                string finalAc3 = int.TryParse(inputAc3, out int num) ? num.ToString("D3") : inputAc3;
                string parentAc1 = (data.AC1 ?? "").Trim();

                // Check duplicate name under this parent account in current company
                var existsInCurrent = await _context.GLChart3.AnyAsync(x =>
                    x.Id != model.Id &&
                    x.GLChart1Id == data.GLChart1Id &&
                    (x.CompanyId == data.CompanyId || x.CoCode == data.CoCode) &&
                    x.Name.ToLower() == trimmedName.ToLower());

                if (existsInCurrent)
                {
                    return Json(new { success = false, message = $"Detail '{trimmedName}' already exists under this account!" });
                }

                // Check duplicate AC1 + AC3 in current company
                var dupInCurrent = await _context.GLChart3.FirstOrDefaultAsync(x =>
                    x.Id != data.Id &&
                    (x.CompanyId == data.CompanyId || x.CoCode == data.CoCode) &&
                    x.AC1 == data.AC1 &&
                    x.AC3 == finalAc3);

                if (dupInCurrent != null)
                {
                    return Json(new { success = false, message = $"Account code {parentAc1}-{finalAc3} is already in use by '{dupInCurrent.Name}' in this company!" });
                }

                // If this detail is linked across companies, validate and sync changes
                if (data.LinkedGroupId.HasValue)
                {
                    var linkedRecords = await _context.GLChart3
                        .Where(x => x.LinkedGroupId == data.LinkedGroupId.Value)
                        .ToListAsync();

                    var linkedIds = linkedRecords.Select(lr => lr.Id).ToList();

                    // Check if new code conflicts in any linked company
                    foreach (var linked in linkedRecords)
                    {
                        var conflict = await _context.GLChart3.FirstOrDefaultAsync(x =>
                            !linkedIds.Contains(x.Id) &&
                            (x.CompanyId == linked.CompanyId || x.CoCode == linked.CoCode) &&
                            x.AC1 == linked.AC1 &&
                            x.AC3 == finalAc3);

                        if (conflict != null)
                        {
                            var comp = await _context.Companies.FirstOrDefaultAsync(c => c.Id == linked.CompanyId || c.Cocode == linked.CoCode);
                            string compName = comp?.Name ?? linked.CoCode ?? "linked company";
                            return Json(new { success = false, message = $"Cannot change code to {finalAc3}: Code {parentAc1}-{finalAc3} is already in use in company '{compName}' (by '{conflict.Name}')!" });
                        }
                    }

                    // Sync AC3, ACC, and Name across all linked records
                    foreach (var linked in linkedRecords)
                    {
                        linked.Name = trimmedName;
                        linked.AC3 = finalAc3;
                        linked.ACC = (linked.AC1?.Trim() ?? "") + finalAc3;
                    }
                }
                else
                {
                    data.Name = trimmedName;
                    data.AC3 = finalAc3;
                    data.ACC = parentAc1 + finalAc3;
                }

                await _context.SaveChangesAsync();

                await _audit.LogAsync("Update", "GLChart3", data.Id.ToString(), $"Updated Detail: {data.Name} ({data.ACC})");

                return Json(new { success = true, message = $"Detail '{trimmedName}' (Code: {parentAc1}-{finalAc3}) updated successfully!" });
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
                    .OrderBy(x => x.AC1)
                    .ThenBy(x => x.AC3)
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
                        .OrderBy(x => x.AC1)
                        .ThenBy(x => x.AC3)
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
        private string GenerateAC1(bool acrossAllCompanies = false)
        {
            var query = _context.GLChart1.AsQueryable();
            if (!acrossAllCompanies)
            {
                int companyId = User.GetCompanyId();
                string companyCode = User.GetCompanyCode();
                query = query.Where(x => x.CompanyId == companyId || x.CoCode == companyCode);
            }

            var list = query.Select(x => x.AC1).ToList();
            int max = 0;
            foreach (var code in list)
            {
                if (int.TryParse(code?.Trim(), out int current) && current > max)
                {
                    max = current;
                }
            }

            int candidate = max + 1;
            string candidateAc1 = candidate.ToString("D3");
            while (query.Any(x => x.AC1 == candidateAc1))
            {
                candidate++;
                candidateAc1 = candidate.ToString("D3");
            }

            return candidateAc1;
        }

        private string GenerateAC3AcrossAllCompanies(string ac1)
        {
            var list = _context.GLChart3
                .Where(x => x.AC1 == ac1)
                .Select(x => x.AC3)
                .ToList();

            int max = 0;
            foreach (var code in list)
            {
                if (int.TryParse(code?.Trim(), out int current) && current > max)
                {
                    max = current;
                }
            }

            int candidate = max + 1;
            string candidateAc3 = candidate.ToString("D3");

            // Ensure candidateAc3 does NOT exist in any company under this AC1
            while (_context.GLChart3.Any(x => x.AC1 == ac1 && x.AC3 == candidateAc3))
            {
                candidate++;
                candidateAc3 = candidate.ToString("D3");
            }

            return candidateAc3;
        }

        private string GenerateAC3ForCompany(string ac1, int companyId, string companyCode)
        {
            var list = _context.GLChart3
                .Where(x => (x.CompanyId == companyId || x.CoCode == companyCode) && x.AC1 == ac1)
                .Select(x => x.AC3)
                .ToList();

            int max = 0;
            foreach (var code in list)
            {
                if (int.TryParse(code?.Trim(), out int current) && current > max)
                {
                    max = current;
                }
            }

            int candidate = max + 1;
            string candidateAc3 = candidate.ToString("D3");

            // Ensure candidateAc3 does NOT exist in this company under this AC1
            while (_context.GLChart3.Any(x => (x.CompanyId == companyId || x.CoCode == companyCode) && x.AC1 == ac1 && x.AC3 == candidateAc3))
            {
                candidate++;
                candidateAc3 = candidate.ToString("D3");
            }

            return candidateAc3;
        }

        private string GenerateAC3(int accountId)
        {
            var glChart1 = _context.GLChart1.Find(accountId);
            if (glChart1 == null) return "001";

            return GenerateAC3ForCompany(glChart1.AC1 ?? "", glChart1.CompanyId, glChart1.CoCode ?? "");
        }
    }
}
