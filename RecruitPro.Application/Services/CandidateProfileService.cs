using System;
using System.Threading.Tasks;
using System.IO;
using AutoMapper;
using RecruitPro.Application.DTOs.Response;
using RecruitPro.Application.DTOs.Request.Candidate;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Application.Interfaces.IServices;
using RecruitPro.Application.Interfaces;
using RecruitPro.Domain.Entities;
namespace RecruitPro.Application.Services
{
    public class CandidateProfileService : ICandidateProfileService
    {
        private readonly ICandidateProfileRepository _candidateRepository;
        private readonly IUserRepository _userRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFileStorageService _fileStorage;
        private readonly IMapper _mapper;

        public CandidateProfileService(
            ICandidateProfileRepository candidateRepository,
            IUserRepository userRepository,
            IUnitOfWork unitOfWork,
            IFileStorageService fileStorage,
            IMapper mapper)
        {
            _candidateRepository = candidateRepository;
            _userRepository = userRepository;
            _unitOfWork = unitOfWork;
            _fileStorage = fileStorage;
            _mapper = mapper;
        }

        public async Task<ApiResponse<CandidateRegisterResponseDto>> RegisterAsync(CandidateRegisterRequest request, Stream? resumeStream, string? resumeFileName, string? resumeContentType = null)
        {
            string? uploadedObjectName = null;
            await _unitOfWork.BeginTransactionAsync();
            try
            {
                // create user
                var user = new User
                {
                    Id = Guid.NewGuid(),
                    Email = request.UserInfo.Email,
                    FullName = request.UserInfo.FullName,
                    Phone = request.UserInfo.Phone,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.UserInfo.PasswordHash),
                    CreatedAt = DateTime.Now
                };


                await _userRepository.AddAsync(user);


                // create candidate profile if provided
                CandidateProfile? profile = null;
                if (request.Profile != null)
                {
                    string? resumeUrl = null;
                    if (resumeStream != null && !string.IsNullOrWhiteSpace(resumeFileName))
                    {
                        uploadedObjectName = $"resumes/{user.Id}/{Guid.NewGuid()}_{Path.GetFileName(resumeFileName)}";
                        resumeUrl = await _fileStorage.UploadFileAsync(
                            resumeStream,
                            uploadedObjectName,
                            resumeContentType ?? "application/octet-stream");
                    }

                    profile = new CandidateProfile
                    {
                        Id = Guid.NewGuid(),
                        UserId = user.Id,
                        CurrentPosition = request.Profile.CurrentPosition,
                        ExperienceYears = request.Profile.ExperienceYears,
                        Education = request.Profile.Education,
                        Address = request.Profile.Address,
                        Bio = request.Profile.Bio,
                        GithubUrl = request.Profile.GitHubUrl,
                        LinkedinUrl = request.Profile.LinkedInUrl,
                        ResumeUrl = resumeUrl
                    };

                    await _candidateRepository.SaveAsync(profile);
                }

                // single commit
                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitAsync();

                var response = _mapper.Map<CandidateRegisterResponseDto>(user, options =>
                {
                    options.Items["CandidateProfileId"] = profile?.Id ?? Guid.Empty;
                });

                return ApiResponse<CandidateRegisterResponseDto>.Created(response);
            }
            catch
            {
                await _unitOfWork.RollbackAsync();

                // best-effort cleanup uploaded file if exists
                try
                {
                    if (!string.IsNullOrWhiteSpace(uploadedObjectName))
                    {
                        await _fileStorage.DeleteFileAsync(uploadedObjectName);
                    }
                }
                catch
                {
                    // ignore cleanup errors
                }

                throw;
            }
        }
    }
}
