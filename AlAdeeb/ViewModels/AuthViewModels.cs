using System.ComponentModel.DataAnnotations;

namespace AlAdeeb.ViewModels
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "رقم الجوال أو اسم المستخدم مطلوب")]
        [Display(Name = "رقم الجوال")]
        public string Username { get; set; }

        [Required(ErrorMessage = "كلمة المرور مطلوبة")]
        [DataType(DataType.Password)]
        [Display(Name = "كلمة المرور")]
        public string Password { get; set; }

        public bool RememberMe { get; set; }
    }

    // --- نموذج إدخال رمز التحقق ---
    public class VerifyCodeViewModel
    {
        [Required]
        public string Username { get; set; }

        [Required(ErrorMessage = "يرجى إدخال رمز التحقق")]
        [StringLength(4, MinimumLength = 4, ErrorMessage = "الرمز يتكون من 4 أرقام")]
        [Display(Name = "رمز التحقق (4 أرقام)")]
        public string Code { get; set; }

        public bool RememberMe { get; set; }
    }

    public class RegisterViewModel
    {
        [Required(ErrorMessage = "الاسم الكامل مطلوب")]
        [Display(Name = "الاسم الثلاثي")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "رقم الجوال مطلوب")]
        [Phone(ErrorMessage = "صيغة رقم الجوال غير صحيحة")]
        [Display(Name = "رقم الجوال (واتساب)")]
        public string PhoneNumber { get; set; }

        [Required(ErrorMessage = "كلمة المرور مطلوبة")]
        [StringLength(100, ErrorMessage = "يجب أن تكون كلمة المرور 6 أحرف على الأقل.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "كلمة المرور")]
        public string Password { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "تأكيد كلمة المرور")]
        [Compare("Password", ErrorMessage = "كلمة المرور غير متطابقة.")]
        public string ConfirmPassword { get; set; }
    }

   
}