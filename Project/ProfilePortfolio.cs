namespace Project
{
    public class ProfilePortfolio
    {
        public string PortfolioName { get; set; }
        public string Description { get; set; }
        public string CreationDate { get; set; }
        public ProfilePortfolio(string portfolioName, string description, string creationDate) 
        {
            PortfolioName= portfolioName;
            Description= description;
            CreationDate= creationDate;
        }
    }
}
