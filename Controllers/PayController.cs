using Microsoft.AspNetCore.Mvc;
using PaystackIntegration.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PayStack.Net;
using System.Threading.Tasks;
using System.Linq;
using System;
using Microsoft.Extensions.Configuration;

namespace PaystackIntegration.Controllers
{
	[Route("api/[controller]")]
	[ApiController]
	public class PayController : ControllerBase
	{
		private readonly PayStackApi _paystack;
		private readonly string _token;
		private readonly IConfiguration _configuration;
		private readonly AppDbContext _context;

		public PayController(IConfiguration configuration, AppDbContext context)
		{
			_configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
			_context = context ?? throw new ArgumentNullException(nameof(context));
			_token = _configuration["Payment:PaystackSK"] ?? throw new InvalidOperationException("Paystack secret key is not configured.");
			_paystack = new PayStackApi(_token);
		}

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

		[HttpGet("getall")]
		public async Task<IActionResult> GetAllSuccessfulTransactions()
		{
			var transactions = await _context.Transactions
				.Where(t => t.Status)
				.ToListAsync();

			return Ok(transactions);
		}

		[HttpGet("transactions/list")]
		public async Task<IActionResult> ListTransactions(int perPage = 50, int page = 1)
		{
			var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.paystack.co/transaction?perPage={perPage}&page={page}");
			request.Headers.Add("Authorization", $"Bearer {_token}");
			var response = await SendRequestAsync(request);
			return Content(response, "application/json");
		}

		[HttpGet("transactions/{id}")]
		public async Task<IActionResult> FetchTransaction(long id)
		{
			var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.paystack.co/transaction/{id}");
			request.Headers.Add("Authorization", $"Bearer {_token}");
			var response = await SendRequestAsync(request);
			return Content(response, "application/json");
		}

		[HttpPost("transactions/charge")]
		public async Task<IActionResult> ChargeAuthorization([FromBody] ChargeAuthorizationModel model)
		{
			var request = new HttpRequestMessage(HttpMethod.Post, "https://api.paystack.co/transaction/charge_authorization")
			{
				Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(new
				{
					amount = model.Amount,
					email = model.Email,
					authorization_code = model.AuthorizationCode
				}), System.Text.Encoding.UTF8, "application/json")
			};
			request.Headers.Add("Authorization", $"Bearer {_token}");
			var response = await SendRequestAsync(request);
			return Content(response, "application/json");
		}

		[HttpGet("transactions/timeline/{id_or_reference}")]
		public async Task<IActionResult> ViewTransactionTimeline(string id_or_reference)
		{
			var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.paystack.co/transaction/timeline/{id_or_reference}");
			request.Headers.Add("Authorization", $"Bearer {_token}");
			var response = await SendRequestAsync(request);
			return Content(response, "application/json");
		}

		[HttpGet("transactions/totals")]
		public async Task<IActionResult> TransactionTotals()
		{
			var request = new HttpRequestMessage(HttpMethod.Get, "https://api.paystack.co/transaction/totals");
			request.Headers.Add("Authorization", $"Bearer {_token}");
			var response = await SendRequestAsync(request);
			return Content(response, "application/json");
		}

		[HttpGet("transactions/export")]
		public async Task<IActionResult> ExportTransactions([FromHeader]int perPage, [FromHeader] int page)
		{
			var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.paystack.co/transaction/export?perPage={perPage}&page={page}");
			request.Headers.Add("Authorization", $"Bearer {_token}");
			var response = await SendRequestAsync(request);
			return Content(response, "application/json");
		}

		[HttpPost("transactions/partial_debit")]
		public async Task<IActionResult> PartialDebit([FromBody] PartialDebitModel model)
		{
			var request = new HttpRequestMessage(HttpMethod.Post, "https://api.paystack.co/transaction/partial_debit")
			{
				Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(new
				{
					authorization_code = model.AuthorizationCode,
					currency = model.Currency,
					amount = model.Amount,
					email = model.Email
				}), System.Text.Encoding.UTF8, "application/json")
			};
			request.Headers.Add("Authorization", $"Bearer {_token}");
			var response = await SendRequestAsync(request);
			return Content(response, "application/json");
		}

		private async Task<string> SendRequestAsync(HttpRequestMessage request)
		{
			using var httpClient = new HttpClient();
			var response = await httpClient.SendAsync(request);
			response.EnsureSuccessStatusCode();
			return await response.Content.ReadAsStringAsync();
		}

		private static int GenerateTransactionReference()
		{
			var random = new Random((int)DateTime.Now.Ticks);
			return random.Next(100000000, 999999999);
		}
	}

	public class ChargeAuthorizationModel
	{
		public string Email { get; set; } = string.Empty;
		public string Amount { get; set; } = string.Empty;
		public string AuthorizationCode { get; set; } = string.Empty;
	}

	public class PartialDebitModel
	{
		public string AuthorizationCode { get; set; } = string.Empty;
		public string Currency { get; set; } = string.Empty;
		public string Amount { get; set; } = string.Empty;
		public string Email { get; set; } = string.Empty;
	}
}
