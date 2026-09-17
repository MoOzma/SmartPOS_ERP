using System.ComponentModel.DataAnnotations;

namespace SmartPOS_ERP.Security
{
    public sealed class StrongPasswordAttribute : ValidationAttribute
    {
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (!PasswordRules.TryValidate(value as string, out var error))
            {
                return new ValidationResult(error);
            }

            return ValidationResult.Success;
        }
    }
}
