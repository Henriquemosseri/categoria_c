using Dapper;
using Domain;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using Newtonsoft.Json;
using Repository;
using StackExchange.Redis;

namespace performance_cache.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class VehicleController : ControllerBase
    {
        private const string cacheKey = "vehicles-cache";
        private const string redisConnection = "localhost:6379";
        private readonly IVehicleRepository vehicleRepository;
        private const string connectionString = "";
        public VehicleController(IVehicleRepository vehicleRepository)
        {
            //recebe a injeção de dependência da repository
            this.vehicleRepository = vehicleRepository;
        }
        // GET: api/vehicle
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var redis = ConnectionMultiplexer.Connect(redisConnection);
            IDatabase dbRedis = redis.GetDatabase();
            await dbRedis.KeyExpireAsync(cacheKey, TimeSpan.FromMinutes(20));

            string cachedVehicles = await dbRedis.StringGetAsync(cacheKey);
            if (!string.IsNullOrEmpty(cachedVehicles))
            {
                return Ok(cachedVehicles);
            }


            string sql = "SELECT id, brand, model, year, plate, color FROM vehicle;";
            var vehicleList = await vehicleRepository.GetAllVehicle();
            var vehicleListJson = JsonConvert.SerializeObject(vehicleList);

            await dbRedis.StringSetAsync(cacheKey, vehicleListJson);

            return Ok(vehicleList);
        }

        // POST: api/vehicle
        [HttpPost]
        public async Task<IActionResult> Post([FromBody] Vehicle vehicle)
        {
            if (vehicle == null)
                return BadRequest("Veículo inválido.");

            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();

            string sql = @"
                INSERT INTO vehicle (brand, model, year, plate, color)
                VALUES (@Brand, @Model, @Year, @Plate, @Color);
                SELECT LAST_INSERT_ID();
            ";

            int newId = await connection.QuerySingleAsync<int>(sql, vehicle);
            vehicle.Id = newId;

            await InvalidateCache();

            return CreatedAtAction(nameof(Get), new { id = newId }, vehicle);
        }

        // PUT: api/vehicle/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> Put(int id, [FromBody] Vehicle vehicle)
        {
            if (vehicle == null)
                return BadRequest("Veículo não fornecido.");

            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();
            vehicle.Id = id;

            string sql = @"
                UPDATE vehicle
                SET brand = @Brand,
                    model = @Model,
                    year = @Year,
                    plate = @Plate,
                    color = @Color
                WHERE id = @Id;
            ";

            int affectedRows = await connection.ExecuteAsync(sql, vehicle);

            if (affectedRows == 0)
                return NotFound("Veículo não encontrado para atualização.");

            await InvalidateCache();
            return NoContent();
        }

        // DELETE: api/vehicle/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            if (id <= 0)
                return BadRequest("ID inválido.");

            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();

            string sql = "DELETE FROM vehicle WHERE id = @Id;";
            int deletedRows = await connection.ExecuteAsync(sql, new { id });

            await InvalidateCache();

            if (deletedRows == 0)
                return NotFound("Veículo não encontrado para exclusão.");

            return NoContent();
        }

        private async Task InvalidateCache()
        {
            var redis = ConnectionMultiplexer.Connect(redisConnection);
            IDatabase dbRedis = redis.GetDatabase();
            await dbRedis.KeyDeleteAsync(cacheKey);
        }
    }
}
