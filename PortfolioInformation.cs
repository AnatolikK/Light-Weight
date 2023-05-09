namespace Project
{
    public class PortfolioInformation
    {
        public string AuthorName { get; set; }
        public string Description { get; set; }
        public string CreationDate { get; set; }
        public string NameOfPortfolio { get; set; }
        public string WorkFile { get; set; }
        public int Rating { get; set; }
        public CommentaryInfo[] Commentaries { get; set; }
    }
}
