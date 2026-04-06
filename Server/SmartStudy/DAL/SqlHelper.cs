using Microsoft.Data.SqlClient;
using System.Data;

namespace SmartStudy.DAL;

public class SqlHelper
{
    private readonly string _connectionString;

    public SqlHelper(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("SmartStudyDb")
            ?? throw new InvalidOperationException("SmartStudyDb connection string not configured.");
    }

    public SqlConnection CreateConnection()
    {
        return new SqlConnection(_connectionString);
    }

    public async Task<List<T>> QueryAsync<T>(string spName, Func<SqlDataReader, T> mapper, params SqlParameter[] parms)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = CreateCommand(conn, spName, parms);
        await using var reader = await cmd.ExecuteReaderAsync();
        var results = new List<T>();
        while (await reader.ReadAsync())
            results.Add(mapper(reader));
        return results;
    }

    public async Task<T?> QuerySingleAsync<T>(string spName, Func<SqlDataReader, T> mapper, params SqlParameter[] parms) where T : class
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = CreateCommand(conn, spName, parms);
        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
            return mapper(reader);
        return null;
    }

    public async Task<T> QuerySingleValueAsync<T>(string spName, T defaultValue, params SqlParameter[] parms)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = CreateCommand(conn, spName, parms);
        var result = await cmd.ExecuteScalarAsync();
        if (result == null || result == DBNull.Value)
            return defaultValue;
        return (T)Convert.ChangeType(result, typeof(T));
    }

    public async Task<int> ExecuteAsync(string spName, params SqlParameter[] parms)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = CreateCommand(conn, spName, parms);
        return await cmd.ExecuteNonQueryAsync();
    }

    public async Task<object?> ScalarAsync(string spName, params SqlParameter[] parms)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = CreateCommand(conn, spName, parms);
        var result = await cmd.ExecuteScalarAsync();
        return result == DBNull.Value ? null : result;
    }

    public async Task QueryMultipleAsync(string spName, Action<SqlDataReader> handler, params SqlParameter[] parms)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = CreateCommand(conn, spName, parms);
        await using var reader = await cmd.ExecuteReaderAsync();
        handler(reader);
    }

    // Overloads that accept an existing connection + transaction for multi-step operations
    public async Task<List<T>> QueryAsync<T>(SqlConnection conn, SqlTransaction? tx, string spName, Func<SqlDataReader, T> mapper, params SqlParameter[] parms)
    {
        await using var cmd = CreateCommand(conn, spName, parms, tx);
        await using var reader = await cmd.ExecuteReaderAsync();
        var results = new List<T>();
        while (await reader.ReadAsync())
            results.Add(mapper(reader));
        return results;
    }

    public async Task<T?> QuerySingleAsync<T>(SqlConnection conn, SqlTransaction? tx, string spName, Func<SqlDataReader, T> mapper, params SqlParameter[] parms) where T : class
    {
        await using var cmd = CreateCommand(conn, spName, parms, tx);
        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
            return mapper(reader);
        return null;
    }

    public async Task<int> ExecuteAsync(SqlConnection conn, SqlTransaction? tx, string spName, params SqlParameter[] parms)
    {
        await using var cmd = CreateCommand(conn, spName, parms, tx);
        return await cmd.ExecuteNonQueryAsync();
    }

    public async Task<object?> ScalarAsync(SqlConnection conn, SqlTransaction? tx, string spName, params SqlParameter[] parms)
    {
        await using var cmd = CreateCommand(conn, spName, parms, tx);
        var result = await cmd.ExecuteScalarAsync();
        return result == DBNull.Value ? null : result;
    }

    private static SqlCommand CreateCommand(SqlConnection conn, string spName, SqlParameter[] parms, SqlTransaction? tx = null)
    {
        var cmd = new SqlCommand(spName, conn)
        {
            CommandType = CommandType.StoredProcedure
        };
        if (tx != null) cmd.Transaction = tx;
        if (parms.Length > 0) cmd.Parameters.AddRange(parms);
        return cmd;
    }

    // Helper to create SqlParameter with nullable value
    public static SqlParameter Param(string name, object? value)
        => new(name, value ?? DBNull.Value);

    public static SqlParameter Param(string name, object? value, SqlDbType type)
        => new(name, type) { Value = value ?? DBNull.Value };
}
