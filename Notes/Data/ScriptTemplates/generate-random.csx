// Generate Random String
// Generates random strings, numbers, or passwords

var random = new Random();

string RandomString(int length, string chars)
{
    return new string(Enumerable.Range(0, length)
        .Select(_ => chars[random.Next(chars.Length)])
        .ToArray());
}

var alphanumeric = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
var hex = "0123456789abcdef";
var passwordChars = alphanumeric + "!@#$%^&*()-_=+[]{}|;:,.<>?";

return new {
    Random_16_Chars = RandomString(16, alphanumeric),
    Random_32_Chars = RandomString(32, alphanumeric),
    Hex_16 = RandomString(16, hex),
    Hex_32 = RandomString(32, hex),
    Password_16 = RandomString(16, passwordChars),
    Random_Number = random.Next(1, 1000000)
};
