namespace PaystackIntegration.Model
{
	public class Transaction
	{
		public int Id { get; set; }
		public string TxnId { get; set; } = Guid.NewGuid().ToString();
		public string Name { get; set; } = string.Empty;
		public int Amount { get; set; }
		public string TransactionReference { get; set; } = string.Empty;
		public string Email { get; set; } = string.Empty;
		public bool Status { get; set; } = false;
		public DateTime TransactionDate { get; set; } = DateTime.UtcNow;
	}
}
