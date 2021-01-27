namespace MerchLoad
{
    class Merchant
    {
        public string merchantID;
        public string partnerID;
        public string account;
        public string paymentSystem;
        public string amount;
        public string orderID;
        public string date;

        public Merchant(string merch, string part, string acc, string ps)
        {
            merchantID = merch;
            partnerID = part;
            account = acc;
            paymentSystem = ps;
        }

        public Merchant() { }
    }
    
}
