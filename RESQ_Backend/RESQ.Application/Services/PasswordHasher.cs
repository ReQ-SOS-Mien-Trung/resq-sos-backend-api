namespace RESQ.Application.Services
{
    public static class PasswordHasher
    {
        public static string HashPassword(string password)
        {
            var salt = System.Security.Cryptography.RandomNumberGenerator.GetBytes(16);
            using var derive = new System.Security.Cryptography.Rfc2898DeriveBytes(password, salt, 10000, System.Security.Cryptography.HashAlgorithmName.SHA256);
            var hash = derive.GetBytes(32);
            var res = new byte[49];
            System.Buffer.BlockCopy(salt, 0, res, 1, 16);
            System.Buffer.BlockCopy(hash, 0, res, 17, 32);
            res[0] = 0x00; // version
            return System.Convert.ToBase64String(res);
        }

        public static bool Verify(string hashed, string password)
        {
            try
            {
                var bytes = System.Convert.FromBase64String(hashed);
                var salt = new byte[16];
                System.Buffer.BlockCopy(bytes, 1, salt, 0, 16);
                using var derive = new System.Security.Cryptography.Rfc2898DeriveBytes(password, salt, 10000, System.Security.Cryptography.HashAlgorithmName.SHA256);
                var hash = derive.GetBytes(32);
                for (int i = 0; i < 32; i++)
                {
                    if (bytes[17 + i] != hash[i]) return false;
                }
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
