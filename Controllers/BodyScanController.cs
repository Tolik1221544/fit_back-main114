using FitnessTracker.API.DTOs;
using FitnessTracker.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FitnessTracker.API.Controllers
{
    [ApiController]
    [Route("api/body-scan")]
    [Authorize]
    public class BodyScanController : ControllerBase
    {
        private readonly IBodyScanService _bodyScanService;

        public BodyScanController(IBodyScanService bodyScanService)
        {
            _bodyScanService = bodyScanService;
        }

        [HttpGet]
        public async Task<IActionResult> GetBodyScans([FromQuery] DateTime? startDate = null, [FromQuery] DateTime? endDate = null)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var scans = await _bodyScanService.GetUserBodyScansAsync(userId, startDate, endDate);
                return Ok(scans);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("{scanId}")]
        public async Task<IActionResult> GetBodyScan(string scanId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var scan = await _bodyScanService.GetBodyScanByIdAsync(userId, scanId);
                if (scan == null)
                    return NotFound();

                return Ok(scan);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> AddBodyScan([FromBody] AddBodyScanRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var scan = await _bodyScanService.AddBodyScanAsync(userId, request);
                return Ok(scan);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPut("{scanId}")]
        public async Task<IActionResult> UpdateBodyScan(string scanId, [FromBody] UpdateBodyScanRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var updatedScan = await _bodyScanService.UpdateBodyScanAsync(userId, scanId, request);
                return Ok(updatedScan);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpDelete("{scanId}")]
        public async Task<IActionResult> DeleteBodyScan(string scanId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                await _bodyScanService.DeleteBodyScanAsync(userId, scanId);
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("comparison")]
        public async Task<IActionResult> GetBodyScanComparison([FromQuery] string? previousScanId = null)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var comparison = await _bodyScanService.GetBodyScanComparisonAsync(userId, previousScanId);
                return Ok(comparison);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}