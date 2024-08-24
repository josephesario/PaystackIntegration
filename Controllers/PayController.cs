using Microsoft.AspNetCore.Mvc;
using PaystackIntegration.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PayStack.Net;


namespace PaystackIntegration.Controllers
{
	[Route("api/[controller]")]
	[ApiController]
	public class PayController : Controller
	{
		
	
			private readonly PayStackApi paystack;
			private readonly string token;
			private readonly IConfiguration _configuration;
			private readonly AppDbContext _context;

			public PayController(IConfiguration configuration, AppDbContext context)
			{
				_configuration = configuration;
				_context = context;
				token = _configuration["Payment:PaystackSK"] ?? string.Empty;
				paystack = new PayStackApi(token);
			}

			[HttpPost("donate")]
			public async Task<IActionResult> Index(DonateViewModel model)
			{
				TransactionInitializeRequest request = new()
				{
					AmountInKobo = model.Amount * 100,
					Email = model.Email,
					Reference = Generate().ToString(),
					Currency = "Ghs",
					CallbackUrl = "http://localhost:7293/payment/verify"
				};

				TransactionInitializeResponse response = paystack.Transactions.Initialize(request);
				if (response.Status)
				{
					var transaction = new Transaction()
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
				TransactionVerifyResponse response = paystack.Transactions.Verify(reference);
				if (response.Data.Status == "success")
				{
					var transaction = await _context.Transactions.Where(c => c.TransactionReference == reference).FirstOrDefaultAsync();
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
			public async Task<IActionResult> GetAllSuccess()
			{
				var transactions = await _context.Transactions.Where(c => c.Status == true).ToListAsync();
				return Ok(transactions);
			}

			public static int Generate()
			{
				Random rand = new Random((int)DateTime.Now.Ticks);
				return rand.Next(100000000, 999999999);
			}
		}
	
}
