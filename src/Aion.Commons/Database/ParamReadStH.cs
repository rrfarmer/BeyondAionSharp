using MySqlConnector;

namespace Aion.Commons.Database
{
    /// <summary>
    /// Java parity: commons/database/ParamReadStH (@author Disturbing). Read statement handler that can set query params before execution.
    /// JDBC PreparedStatement -> MySqlCommand.
    /// </summary>
    public interface ParamReadStH : ReadStH
    {
        /// <summary>
        /// Enables coder to manually modify statement parameters.
        /// </summary>
        void SetParams(MySqlCommand stmt);
    }
}
