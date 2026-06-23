using AutoMapper;
using RecruitPro.Application.DTOs.Response;
using RecruitPro.Domain.Entities;

namespace RecruitPro.Application.Mappings
{
    public class UserProfile : Profile
    {
        public UserProfile()
        {
            CreateMap<User, UserDto>()
                .ForMember(
                    dest => dest.Roles,
                    opt => opt.MapFrom(
                        src => src.UserRoles
                            .Select(ur => ur.Role.Name)
                            .ToList()));

            CreateMap<User, LoginResponseDto>()
                .ForMember(dest => dest.User, opt => opt.MapFrom(src => src))
                .ForMember(
                    dest => dest.AccessToken,
                    opt => opt.MapFrom((src, dest, destMember, context) =>
                        context.Items.TryGetValue("AccessToken", out var accessToken)
                            ? accessToken as string ?? string.Empty
                            : string.Empty))
                .ForMember(
                    dest => dest.RefreshToken,
                    opt => opt.MapFrom((src, dest, destMember, context) =>
                        context.Items.TryGetValue("RefreshToken", out var refreshToken)
                            ? refreshToken as string ?? string.Empty
                            : string.Empty));

            CreateMap<User, CandidateRegisterResponseDto>()
                .ForMember(dest => dest.UserId, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.Username, opt => opt.MapFrom(src => src.Username))
                .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.Email))
                .ForMember(dest => dest.FullName, opt => opt.MapFrom(src => src.FullName))
                .ForMember(
                    dest => dest.CandidateProfileId,
                    opt => opt.MapFrom((src, dest, destMember, context) =>
                        context.Items.TryGetValue("CandidateProfileId", out var candidateProfileId)
                            ? candidateProfileId is Guid id ? id : Guid.Empty
                            : Guid.Empty));
        }
    }
}
