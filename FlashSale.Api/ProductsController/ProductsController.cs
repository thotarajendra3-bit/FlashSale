using Microsoft.AspNetCore.Mvc;
using FlashSale.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace FlashSale.Api.ProductsController
{
    [ApiController]
    [Route("api/products")]
    public class ProductsController : Controller
    {
        private readonly AppDbContext _context;
        public ProductsController(AppDbContext context)
        {
            _context = context;
        }
        [HttpGet("{id}/stock")]
        public async Task<IActionResult> GetStock(int id)
        {
            var product = await _context.Products
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null)
            {
                return NotFound(new
                {
                    message = "Product not found"
                });
            }

            return Ok(new
            {
                productId = product.Id,
                productName = product.Name,
                stock = product.Stock
            });
        }
    }
}



