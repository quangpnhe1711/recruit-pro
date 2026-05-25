using RecruitPro.Application.DTOs.Response;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Application.Interfaces.IServices;
using RecruitPro.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RecruitPro.Application.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepository;

        public AuthService(IUserRepository userRepository)
        {
            this._userRepository = userRepository;
        }

        public async Task<ApiResponse<LoginResponseDto>> LoginAsync(string email, string password)
        {
            User user = await _userRepository.GetByEmailAsync(email);

            if (user == null)
            {
                throw new Exception("Invalid email or password.");
            }

            var loginResponseDto = new LoginResponseDto();
            loginResponseDto.User = new UserDto
            {
                Id = user.Id,
                Email = user.Email,
                FullName = user.FullName,
                Phone = user.Phone,
                AvatarUrl = user.AvatarUrl,
                Roles = user.UserRoles.Select(ur => ur.Role.Name).ToList()
            };

            return null;
        }
    }
}
