using Microsoft.AspNetCore.Mvc;
using PaystackIntegration.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PayStack.Net;
using System.Threading.Tasks;
using System.Linq;
using System;

namespace PaystackIntegration.Controllers
{
	/// <summary>
	/// Controller for handling Paystack payment transactions.
	/// </summary>
	[Route("api/[controller]")]
	[ApiController]
	public class PayController : ControllerBase
	{
		private readonly PayStackApi _paystack;
		private readonly string _token;
		private readonly IConfiguration _configuration;
		private readonly AppDbContext _context;

		/// <summary>
		/// Initializes a new instance of the <see cref="PayController"/> class.
		/// </summary>
		/// <param name="configuration">The application configuration.</param>
		/// <param name="context">The database context.</param>
		public PayController(IConfiguration configuration, AppDbContext context)
		{
			_configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
			_context = context ?? throw new ArgumentNullException(nameof(context));
			_token = _configuration["Payment:PaystackSK"] ?? throw new InvalidOperationException("Paystack secret key is not configured.");
			_paystack = new PayStackApi(_token);
		}

		/// <summary>
		/// Initiates a donation transaction with Paystack.
		/// </summary>
		/// <param name="model">The donation view model containing transaction details.</param>
		/// <returns>An <see cref="IActionResult"/> representing the result of the operation.</returns>
		[HttpPost("donate")]
		public async Task<IActionResult> Donate(DonateViewModel model)
		{
			if (model == null)
			{
				return BadRequest("Donation model cannot be null.");
			}

			var request = new TransactionInitializeRequest
			{
				AmountInKobo = model.Amount * 100,
				Email = model.Email,
				Reference = GenerateTransactionReference().ToString(),
				Currency = "GHS",
				CallbackUrl = "http://localhost:7293/payment/verify"
			};

			var response = _paystack.Transactions.Initialize(request);
			if (response.Status)
			{
				var transaction = new Transaction
				{
					Amount = model.Amount,
					Email = model.Email,
					TransactionReference = request.Reference,
					Name = model.Name
				};
				await _context.Transactions.AddAsync(transaction);
				await _context.SaveChangesAsync();
				return Ok(response.Data);
			}

			return BadRequest(response.Message);
		}

		/// <summary>
		/// Verifies the status of a transaction with Paystack.
		/// </summary>
		/// <param name="reference">The transaction reference to verify.</param>
		/// <returns>An <see cref="IActionResult"/> representing the result of the operation.</returns>
		[HttpGet("verify")]
		public async Task<IActionResult> Verify(string reference)
		{
			if (string.IsNullOrEmpty(reference))
			{
				return BadRequest("Transaction reference cannot be null or empty.");
			}

			var response = _paystack.Transactions.Verify(reference);
			if (response.Data.Status == "success")
			{
				var transaction = await _context.Transactions
					.Where(t => t.TransactionReference == reference)
					.FirstOrDefaultAsync();

				if (transaction != null)
				{
					transaction.Status = true;
					_context.Transactions.Update(transaction);
					await _context.SaveChangesAsync();
					return Ok(response.Data);
				}
			}

			return BadRequest(response.Data.GatewayResponse);
		}

		/// <summary>
		/// Retrieves all successful transactions.
		/// </summary>
		/// <returns>An <see cref="IActionResult"/> containing the list of successful transactions.</returns>
		[HttpGet("getall")]
		public async Task<IActionResult> GetAllSuccessfulTransactions()
		{
			var transactions = await _context.Transactions
				.Where(t => t.Status)
				.ToListAsync();

			return Ok(transactions);
		}

		/// <summary>
		/// Generates a unique transaction reference.
		/// </summary>
		/// <returns>A random integer representing the transaction reference.</returns>
		private static int GenerateTransactionReference()
		{
			var random = new Random((int)DateTime.Now.Ticks);
			return random.Next(100000000, 999999999);
		}
	}
}
