
using System.Data;
using Microsoft.Data.SqlClient;
using Cw_7_s30976.DTOs; 
using Cw_7_s30976.Exceptions; 

namespace Cw_7_s30976.Services;

public interface IDbService
{
    Task<IEnumerable<TripDto>> GetTripsAsync();
    Task<IEnumerable<TripDto>?> GetTripsForClientAsync(int clientId);
    Task<int> AddClientAsync(ClientDto client);
    Task<bool> RegisterClientToTripAsync(int clientId, int tripId);
    Task<bool> RemoveClientFromTripAsync(int clientId, int tripId);
}

public class DbService(IConfiguration config) : IDbService
{
    private async Task<SqlConnection> GetConnectionAsync()
    {
        var conn = new SqlConnection(config.GetConnectionString("DefaultConnection"));
        if (conn.State != ConnectionState.Open)
            await conn.OpenAsync();
        return conn;
    }

    public async Task<IEnumerable<TripDto>> GetTripsAsync()
    {
        var trips = new Dictionary<int, TripDto>();
        await using var conn = await GetConnectionAsync();

        var sql = @"
            SELECT t.IdTrip, t.Name, t.Description, t.DateFrom, t.DateTo, t.MaxPeople, c.Name AS Country
            FROM Trip t
            JOIN Country_Trip ct ON t.IdTrip = ct.IdTrip
            JOIN Country c ON c.IdCountry = ct.IdCountry";

        await using var command = new SqlCommand(sql, conn);
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var id = reader.GetInt32(0);
            if (!trips.ContainsKey(id))
            {
                trips[id] = new TripDto
                {
                    IdTrip = id,
                    Name = reader.GetString(1),
                    Description = reader.GetString(2),
                    DateFrom = reader.GetDateTime(3),
                    DateTo = reader.GetDateTime(4),
                    MaxPeople = reader.GetInt32(5),
                    Countries = new List<string>()
                };
            }
            trips[id].Countries.Add(reader.GetString(6));
        }

        return trips.Values;
    }

    public async Task<IEnumerable<TripDto>?> GetTripsForClientAsync(int clientId)
    {
        var trips = new Dictionary<int, TripDto>();
        await using var conn = await GetConnectionAsync();

        var sql = @"
            SELECT t.IdTrip, t.Name, t.Description, t.DateFrom, t.DateTo, t.MaxPeople, c.Name AS Country
            FROM Trip t
            JOIN Client_Trip ct ON ct.IdTrip = t.IdTrip
            JOIN Country_Trip ctr ON ctr.IdTrip = t.IdTrip
            JOIN Country c ON c.IdCountry = ctr.IdCountry
            WHERE ct.IdClient = @IdClient";

        await using var command = new SqlCommand(sql, conn);
        command.Parameters.AddWithValue("@IdClient", clientId);
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var id = reader.GetInt32(0);
            if (!trips.ContainsKey(id))
            {
                trips[id] = new TripDto
                {
                    IdTrip = id,
                    Name = reader.GetString(1),
                    Description = reader.GetString(2),
                    DateFrom = reader.GetDateTime(3),
                    DateTo = reader.GetDateTime(4),
                    MaxPeople = reader.GetInt32(5),
                    Countries = new List<string>()
                };
            }
            trips[id].Countries.Add(reader.GetString(6));
        }

        return trips.Values;
    }

    public async Task<int> AddClientAsync(ClientDto client)
    {
        await using var conn = await GetConnectionAsync();
        var sql = @"
            INSERT INTO Client (FirstName, LastName, Email, Telephone, Pesel)
            OUTPUT INSERTED.IdClient
            VALUES (@FirstName, @LastName, @Email, @Telephone, @Pesel)";

        await using var command = new SqlCommand(sql, conn);
        command.Parameters.AddWithValue("@FirstName", client.FirstName);
        command.Parameters.AddWithValue("@LastName", client.LastName);
        command.Parameters.AddWithValue("@Email", client.Email);
        command.Parameters.AddWithValue("@Telephone", client.Telephone);
        command.Parameters.AddWithValue("@Pesel", client.Pesel);

        return (int)(await command.ExecuteScalarAsync())!;
    }

    public async Task<bool> RegisterClientToTripAsync(int clientId, int tripId)
    {
        await using var conn = await GetConnectionAsync();
        await using var tran = (SqlTransaction)await conn.BeginTransactionAsync();

        try
        {
            await using var checkClient = new SqlCommand("SELECT 1 FROM Client WHERE IdClient = @id", conn, tran);
            checkClient.Parameters.AddWithValue("@id", clientId);
            if ((await checkClient.ExecuteScalarAsync()) is null)
            {
                await tran.RollbackAsync();
                throw new NotFoundException($"Client with ID {clientId} not found");
            }

            await using var checkTrip = new SqlCommand("SELECT MaxPeople FROM Trip WHERE IdTrip = @id", conn, tran);
            checkTrip.Parameters.AddWithValue("@id", tripId);
            var maxPeople = await checkTrip.ExecuteScalarAsync();
            if (maxPeople is null)
            {
                await tran.RollbackAsync();
                throw new NotFoundException($"Trip with ID {tripId} not found");
            }

            await using var count = new SqlCommand("SELECT COUNT(*) FROM Client_Trip WHERE IdTrip = @id", conn, tran);
            count.Parameters.AddWithValue("@id", tripId);
            var currentCount = (int)(await count.ExecuteScalarAsync())!;

            if (currentCount >= (int)maxPeople!)
            {
                await tran.RollbackAsync();
                throw new ConflictException($"Trip with ID {tripId} has reached max capacity");
            }

            await using var insert = new SqlCommand(@"
                INSERT INTO Client_Trip (IdClient, IdTrip, RegisteredAt)
                VALUES (@ClientId, @TripId, @Now)", conn, tran);
            insert.Parameters.AddWithValue("@ClientId", clientId);
            insert.Parameters.AddWithValue("@TripId", tripId);
            insert.Parameters.AddWithValue("@Now", DateTime.Now);
            await insert.ExecuteNonQueryAsync();

            await tran.CommitAsync();
            return true;
        }
        catch
        {
            await tran.RollbackAsync();
            throw;
        }
    }

    public async Task<bool> RemoveClientFromTripAsync(int clientId, int tripId)
    {
        await using var conn = await GetConnectionAsync();
        await using var tran = await conn.BeginTransactionAsync();

        await using var check = new SqlCommand("SELECT 1 FROM Client_Trip WHERE IdClient = @c AND IdTrip = @t", conn, (SqlTransaction)tran);
        check.Parameters.AddWithValue("@c", clientId);
        check.Parameters.AddWithValue("@t", tripId);
        if ((await check.ExecuteScalarAsync()) is null)
        {
            await tran.RollbackAsync();
            throw new NotFoundException("ClientTrip registration not found");
        }

        await using var delete = new SqlCommand("DELETE FROM Client_Trip WHERE IdClient = @c AND IdTrip = @t", conn, (SqlTransaction)tran);
        delete.Parameters.AddWithValue("@c", clientId);
        delete.Parameters.AddWithValue("@t", tripId);
        await delete.ExecuteNonQueryAsync();

        await tran.CommitAsync();
        return true;
    }
}