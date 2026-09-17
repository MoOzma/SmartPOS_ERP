namespace SmartPOS_ERP.Security
{
    public static class PasswordRules
    {
        public const int Length = 4;
        public const string InvalidMessage = "الرقم السري يجب أن يكون 4 أرقام.";

        public static bool TryValidate(string? password, out string error)
        {
            if (string.IsNullOrEmpty(password) || password.Length != Length || !password.All(char.IsDigit))
            {
                error = InvalidMessage;
                return false;
            }

            error = string.Empty;
            return true;
        }
    }
}
