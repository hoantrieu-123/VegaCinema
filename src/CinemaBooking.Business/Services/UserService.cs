using CinemaBooking.DataAccess;
using CinemaBooking.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
namespace CinemaBooking.Business.Services;
public class UserService(UserManager<ApplicationUser> users, VegaDbContext db)
{
    public async Task<ApplicationUser> RegisterAsync(string fullName, string email, string password)
    {
        var user = new ApplicationUser { FullName = fullName.Trim(), Email = email.Trim(), UserName = email.Trim() };
        Check(await users.CreateAsync(user, password)); Check(await users.AddToRoleAsync(user, Roles.Customer)); return user;
    }
    public async Task UpdateProfileAsync(ApplicationUser user, string name, string? phone)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 100 || phone?.Length > 25) throw new BusinessException("Thông tin cá nhân không hợp lệ.");
        user.FullName = name.Trim(); user.PhoneNumber = phone; Check(await users.UpdateAsync(user));
    }
    public async Task SaveAdminAsync(string? id, string name, string email, string role, int? clusterId, bool active, string? password, string actorId)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 100 || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email))
            throw new BusinessException("Họ tên và email chưa hợp lệ.");
        if (!Roles.All.Contains(role)) throw new BusinessException("Vai trò không hợp lệ.");
        if (role is Roles.Staff or Roles.Checker or Roles.Manager && (clusterId is null || !await db.CinemaClusters.AnyAsync(x => x.Id == clusterId)))
            throw new BusinessException("Nhân viên và quản lý phải được gán cụm rạp.");
        var user = string.IsNullOrWhiteSpace(id) ? null : await users.FindByIdAsync(id);
        if (!string.IsNullOrWhiteSpace(id) && user is null) throw new BusinessException("Không tìm thấy tài khoản.");
        if (id == actorId && (!active || role != Roles.Admin)) throw new BusinessException("Không thể tự khóa hoặc tự bỏ quyền quản trị.");
        if (user is null)
        {
            user = new ApplicationUser { FullName = name, UserName = email, Email = email, CinemaClusterId = clusterId, IsActive = active };
            Check(await users.CreateAsync(user, password ?? "")); Check(await users.AddToRoleAsync(user, role));
        }
        else
        {
            if (await users.IsInRoleAsync(user, Roles.Admin) && role != Roles.Admin && (await users.GetUsersInRoleAsync(Roles.Admin)).Count <= 1)
                throw new BusinessException("Phải giữ ít nhất một quản trị viên.");
            await using var tx = await db.Database.BeginTransactionAsync();
            user.FullName = name; user.Email = email; user.UserName = email; user.CinemaClusterId = clusterId; user.IsActive = active;
            Check(await users.UpdateAsync(user));
            var oldRoles = await users.GetRolesAsync(user);
            Check(await users.RemoveFromRolesAsync(user, oldRoles)); Check(await users.AddToRoleAsync(user, role));
            Check(await users.UpdateSecurityStampAsync(user));
            await tx.CommitAsync();
        }
    }
    public static void Check(IdentityResult result)
    {
        if (!result.Succeeded) throw new BusinessException(string.Join(" ", result.Errors.Select(x => x.Code switch
        {
            "DuplicateEmail" or "DuplicateUserName" => "Email đã được sử dụng.",
            "PasswordMismatch" => "Mật khẩu hiện tại không đúng.",
            "PasswordTooShort" or "PasswordRequiresNonAlphanumeric" or "PasswordRequiresDigit" or "PasswordRequiresUpper" or "PasswordRequiresLower" => "Mật khẩu cần ít nhất 8 ký tự, chữ hoa, chữ thường, chữ số và ký tự đặc biệt.",
            "InvalidEmail" => "Email không hợp lệ.", _ => "Không thể cập nhật tài khoản. Vui lòng kiểm tra thông tin."
        }).Distinct()));
    }
}
