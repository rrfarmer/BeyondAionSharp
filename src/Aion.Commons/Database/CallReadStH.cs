using MySqlConnector;

namespace Aion.Commons.Database
{
    /// <summary>
    /// Java parity: commons/database/CallReadStH (@author ATracer). Stored-procedure read statement handler. JDBC CallableStatement ->
    /// MySqlCommand (CommandType.StoredProcedure is set by callers/DB.call as needed).
    /// </summary>
    public interface CallReadStH : ReadStH
    {
        /// <summary>
        /// Enables coder to manually modify the callable-statement parameters.
        /// </summary>
        void SetParams(MySqlCommand stmt);
    }
}
