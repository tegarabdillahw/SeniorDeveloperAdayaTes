using Microsoft.AspNetCore.Mvc;
using SeniorDeveloperAdayaTes.Models;
using SeniorDeveloperAdayaTes.Services;
using static SeniorDeveloperAdayaTes.Models.OrderModels;

namespace SeniorDeveloperAdayaTes.Controller
{
    [ApiController]
    [Route("Orders")]
    public class OrdersController : ControllerBase
    {
        private readonly OrderService _service;

        public OrdersController(OrderService service)
        {
            _service = service;
        }
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateOrderRequest request)
        {
            var order = await _service.CreateAsync(request);

            return CreatedAtAction(
                nameof(GetById),
                new { id = order.Id },
                order);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            return Ok(await _service.GetByIdAsync(id));
        }

        [HttpGet]
        public async Task<IActionResult> GetList(
            [FromQuery] OrderStatus? status,
            [FromQuery] long? customerId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            return Ok(await _service.GetListAsync(status, customerId, page, pageSize));
        }

        [HttpPatch("{id:guid}/status")]
        public async Task<IActionResult> UpdateStatus(
            Guid id,
            [FromBody] UpdateOrderStatusRequest request)
        {
            return Ok(await _service.UpdateStatusAsync(id, request.Status!.Value));
        }

        [HttpPost("{id:guid}/cancel")]
        public async Task<IActionResult> Cancel(Guid id)
        {
            return Ok(await _service.CancelAsync(id));
        }
    }
}
