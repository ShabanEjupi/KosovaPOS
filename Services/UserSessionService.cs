using System;
using KosovaPOS.Models;

namespace KosovaPOS.Services
{
    /// <summary>
    /// Manages the current user session and permissions
    /// </summary>
    public class UserSessionService
    {
        private static UserSessionService? _instance;
        private User? _currentUser;
        
        public static UserSessionService Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new UserSessionService();
                }
                return _instance;
            }
        }
        
        private UserSessionService()
        {
            // Initialize with default admin user if no authentication
            _currentUser = new User
            {
                Id = 0,
                Username = "admin",
                FullName = "Administrator",
                Role = "Admin",
                IsActive = true,
                CanManageArticles = true,
                CanManagePurchases = true,
                CanManageUsers = true,
                CanViewReports = true,
                CanModifyPrices = true,
                CanDeleteReceipts = true,
                CanGiveDiscounts = true,
                MaxDiscountPercent = 100
            };
        }
        
        public User? CurrentUser => _currentUser;
        
        public void SetCurrentUser(User user)
        {
            _currentUser = user;
            AuditService.SetCurrentUser(user.Id.ToString(), user.FullName);
        }
        
        public void Logout()
        {
            _currentUser = null;
        }
        
        public bool IsLoggedIn => _currentUser != null;
        
        // Permission checks
        public bool CanManageArticles => _currentUser?.CanManageArticles ?? false;
        public bool CanManagePurchases => _currentUser?.CanManagePurchases ?? false;
        public bool CanManageUsers => _currentUser?.CanManageUsers ?? false;
        public bool CanViewReports => _currentUser?.CanViewReports ?? false;
        public bool CanModifyPrices => _currentUser?.CanModifyPrices ?? false;
        public bool CanDeleteReceipts => _currentUser?.CanDeleteReceipts ?? false;
        public bool CanGiveDiscounts => _currentUser?.CanGiveDiscounts ?? false;
        public decimal MaxDiscountPercent => _currentUser?.MaxDiscountPercent ?? 0;
        
        public bool IsAdmin => _currentUser?.Role == "Admin";
        public bool IsManager => _currentUser?.Role == "Manager" || IsAdmin;
    }
}
