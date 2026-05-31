using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Domain.Entities;
using RecruitPro.Infrastructure.Data;

namespace RecruitPro.Infrastructure.Repositories
{
    public class CandidateProfileRepository: ICandidateProfileRepository
    {
        private readonly AppDbContext _context;

        public CandidateProfileRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<CandidateProfile> SaveAsync(CandidateProfile profile)
        {
            await _context.CandidateProfiles.AddAsync(profile);
            return profile;
        }
    }
}
