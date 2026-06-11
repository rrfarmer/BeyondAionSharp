using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;

namespace Aion.Commons.Database
{
    /// <summary>
    /// Java parity: commons/database/Transaction (@author SoulKeeper). Easy manipulation of multi-table transactions. Not thread-safe;
    /// not fail-safe. JDBC mapping: Connection.setAutoCommit(false) -> con.BeginTransaction(); Connection.commit/rollback ->
    /// MySqlTransaction.Commit/Rollback; java.sql.Savepoint -> savepoint-name string (MySqlTransaction.Save/Rollback/Release by name);
    /// PreparedStatement -> MySqlCommand bound to the transaction. Package-private Java ctor -> internal (instantiated via DB.BeginTransaction).
    /// </summary>
    public class Transaction
    {
        private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(Transaction));

        private readonly MySqlConnection connection;
        private readonly MySqlTransaction transaction;

        /// <summary>
        /// Instantiated via <see cref="DB.BeginTransaction"/>. Disables autocommit by opening an explicit transaction.
        /// </summary>
        internal Transaction(MySqlConnection con)
        {
            this.connection = con;
            this.transaction = connection.BeginTransaction();
        }

        /// <summary>Adds an insert/update query to the transaction.</summary>
        public void InsertUpdate(string sql)
        {
            InsertUpdate(sql, null);
        }

        /// <summary>
        /// Adds an insert/update query to this transaction. Utilizes IUStH for batching and query editing; the handler MUST execute the
        /// query/batch itself (no need to close the statement).
        /// </summary>
        public void InsertUpdate(string sql, IUStH iusth)
        {
            MySqlCommand statement = connection.CreateCommand();
            statement.Transaction = transaction;
            statement.CommandText = sql;
            if (iusth != null)
            {
                iusth.HandleInsertUpdate(statement);
            }
            else
            {
                statement.ExecuteNonQuery();
            }
        }

        /// <summary>Creates a new savepoint (returns its name).</summary>
        public string SetSavepoint(string name)
        {
            transaction.Save(name);
            return name;
        }

        /// <summary>Releases a savepoint of the transaction.</summary>
        public void ReleaseSavepoint(string savepoint)
        {
            transaction.Release(savepoint);
        }

        /// <summary>Commits the transaction.</summary>
        public void Commit()
        {
            Commit(null);
        }

        /// <summary>
        /// Commits the transaction. If rollBackToOnError is null the whole transaction is rolled back on error, otherwise it rolls back
        /// to the named savepoint.
        /// </summary>
        public void Commit(string rollBackToOnError)
        {
            try
            {
                transaction.Commit();
            }
            catch (Exception e)
            {
                log.LogWarning(e, "Error while commiting transaction");

                try
                {
                    if (rollBackToOnError != null)
                    {
                        transaction.Rollback(rollBackToOnError);
                    }
                    else
                    {
                        transaction.Rollback();
                    }
                }
                catch (Exception e1)
                {
                    log.LogError(e1, "Can't rollback transaction");
                }
            }

            transaction.Dispose();
            connection.Close();
        }
    }
}
