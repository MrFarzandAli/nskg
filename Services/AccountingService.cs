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
            // ✅ REMOVE OLD POSTING (IMPORTANT)
            var old = _context.GLTrans
                .Where(x => x.Vono == head.Vono &&
                            x.CompanyId == head.CompanyId &&
                            x.FinancialYearId == head.FinancialYearId);

            _context.GLTrans.RemoveRange(old);

            foreach (var d in details)
            {
                if ((d.Dramt ?? 0) == 0 && (d.Cramt ?? 0) == 0)
                    continue; // skip empty rows

                _context.GLTrans.Add(new GLTrans
                {
                    Vono = head.Vono,
                    Vodate = head.Vodate ?? DateTime.Now,
                    Accode = d.Ac1,
                    Debit = d.Dramt ?? 0,
                    Credit = d.Cramt ?? 0,
                    CompanyId = head.CompanyId,
                    FinancialYearId = head.FinancialYearId,
                    Narration = d.Narration
                });
            }

            _context.SaveChanges();
        }
    }
}
