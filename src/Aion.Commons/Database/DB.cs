using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;

namespace Aion.Commons.Database
{
    /// <summary>
    /// Java parity: commons/database/DB (@author Disturbing). SQL helper over DatabaseFactory connections, with ReadStH/ParamReadStH/
    /// CallReadStH/IUStH callback handlers. JDBC mapping: Connection -> MySqlConnection (DatabaseFactory.GetConnection + Open); PreparedStatement
    /// -> MySqlCommand (CreateCommand + CommandText); CallableStatement -> MySqlCommand; ResultSet -> MySqlDataReader; executeQuery ->
    /// ExecuteReader; executeUpdate -> ExecuteNonQuery; reader instanceof X -> reader is X. prepareStatement's resultSetType/concurrency
    /// args have no ADO.NET analog and are dropped. The executeQuerry misspelling is preserved 1:1. close drops the JDBC isClosed() guard
    /// (no MySqlCommand analog) and closes the command's connection.
    /// </summary>
    public sealed class DB
    {
        private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(DB));

        /// <summary>Prevent instantiation</summary>
        private DB()
        {
        }

        /// <summary>
        /// Executes a select query. Uses ReadStH to set params and read data. Recycles the connection after completion.
        /// </summary>
        public static bool Select(string query, ReadStH reader)
        {
            try
            {
                using MySqlConnection con = DatabaseFactory.GetConnection();
                con.Open();
                using MySqlCommand stmt = con.CreateCommand();
                stmt.CommandText = query;
                if (reader is ParamReadStH p)
                    p.SetParams(stmt);
                using MySqlDataReader rset = stmt.ExecuteReader();
                reader.HandleRead(rset);
            }
            catch (Exception e)
            {
                log.LogError(e, "Error executing select query " + query);
                return false;
            }
            return true;
        }

        /// <summary>Call a stored procedure.</summary>
        public static bool Call(string query, ReadStH reader)
        {
            try
            {
                using MySqlConnection con = DatabaseFactory.GetConnection();
                con.Open();
                using MySqlCommand stmt = con.CreateCommand();
                stmt.CommandText = query;
                if (reader is CallReadStH c)
                    c.SetParams(stmt);
                using MySqlDataReader rset = stmt.ExecuteReader();
                reader.HandleRead(rset);
            }
            catch (Exception e)
            {
                log.LogError(e, "Error calling stored procedure " + query);
                return false;
            }
            return true;
        }

        /// <summary>Executes an insert/update query needing no further modification or batching. Recycles the connection after completion.</summary>
        public static bool InsertUpdate(string query)
        {
            return InsertUpdate(query, null);
        }

        /// <summary>
        /// Executes an insert/update query. Utilizes IUStH for batching and query editing. The handler MUST execute the query/batch
        /// itself (no need to close the statement). Recycles the connection after completion.
        /// </summary>
        public static bool InsertUpdate(string query, IUStH batch)
        {
            try
            {
                using MySqlConnection con = DatabaseFactory.GetConnection();
                con.Open();
                using MySqlCommand stmt = con.CreateCommand();
                stmt.CommandText = query;
                if (batch != null)
                    batch.HandleInsertUpdate(stmt);
                else
                    stmt.ExecuteNonQuery();
            }
            catch (Exception e)
            {
                log.LogError(e, "Failed to execute IU query " + query);
                return false;
            }
            return true;
        }

        /// <summary>Begins a new transaction.</summary>
        public static Transaction BeginTransaction()
        {
            MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            return new Transaction(con);
        }

        /// <summary>
        /// Creates a MySqlCommand bound to a fresh open connection for the given sql. Java's resultSetType/concurrency overload args
        /// have no ADO.NET analog. Returns null if an error happened while creating.
        /// </summary>
        public static MySqlCommand PrepareStatement(string sql)
        {
            return PrepareStatement(sql, 0, 0);
        }

        public static MySqlCommand PrepareStatement(string sql, int resultSetType, int resultSetConcurrency)
        {
            MySqlConnection c = null;
            MySqlCommand ps = null;
            try
            {
                c = DatabaseFactory.GetConnection();
                c.Open();
                ps = c.CreateCommand();
                ps.CommandText = sql;
            }
            catch (Exception e)
            {
                log.LogError(e, "Can't create PreparedStatement for query: " + sql);
                if (c != null)
                {
                    try
                    {
                        c.Close();
                    }
                    catch (Exception e1)
                    {
                        log.LogError(e1, "Can't close connection after exception");
                    }
                }
            }

            return ps;
        }

        /// <summary>Executes a command's non-query and returns the affected-row count, or -1 on error.</summary>
        public static int ExecuteUpdate(MySqlCommand statement)
        {
            try
            {
                return statement.ExecuteNonQuery();
            }
            catch (Exception e)
            {
                log.LogError(e, "Can't execute update for PreparedStatement");
            }

            return -1;
        }

        /// <summary>Executes a command's non-query and closes it and its connection.</summary>
        public static void ExecuteUpdateAndClose(MySqlCommand statement)
        {
            ExecuteUpdate(statement);
            Close(statement);
        }

        /// <summary>Executes a query and returns the reader, or null on error. (Java method name "executeQuerry" preserved.)</summary>
        public static MySqlDataReader ExecuteQuerry(MySqlCommand statement)
        {
            MySqlDataReader rs = null;
            try
            {
                rs = statement.ExecuteReader();
            }
            catch (Exception e)
            {
                log.LogError(e, "Error while executing query");
            }
            return rs;
        }

        /// <summary>Closes the command and its connection.</summary>
        public static void Close(MySqlCommand statement)
        {
            if (statement == null) // e.g. null from a failed DB.PrepareStatement call
                return;
            try
            {
                MySqlConnection c = statement.Connection;
                statement.Dispose();
                c?.Close();
            }
            catch (Exception e)
            {
                log.LogError(e, "Error while closing PreparedStatement");
            }
        }
    }
}
