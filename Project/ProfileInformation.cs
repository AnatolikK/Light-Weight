namespace Project
{
    public class ProfileInformation
    {
        public string Username { get; set; }
        public string Email { get; set; }
        public string Avatar { get; set; }
        public string Description { get; set; }
        public string Date { get; set; }
        public ProfilePortfolio[] Portfolios { get; set; }
        public ProfileInformation(string username, string description, string date, string email=null, string avatar=null, ProfilePortfolio[] portfolios=null)
        {
            Username = username;
            Email = email;
            Avatar = avatar;
            Description = description;
            Date = date;
            Portfolios = portfolios;
        }
    }
}
