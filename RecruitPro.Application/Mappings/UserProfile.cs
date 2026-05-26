using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AutoMapper;
using RecruitPro.Application.DTOs.Response;
using RecruitPro.Domain.Entities;

namespace RecruitPro.Application.Mappings
{
    public class UserProfile : Profile
    {
        public UserProfile()
        {
            CreateMap<User, UserDto>().ForMember(
                dest => dest.Roles,
                opt => opt.MapFrom(
                    src => src.UserRoles
                    .Select(ur => ur.Role.Name)
                    .ToList()
                    )
                );
        }
    }
}
