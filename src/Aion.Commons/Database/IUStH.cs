using MySqlConnector;

namespace Aion.Commons.Database
{
    /// <summary>
    /// Java parity: commons/database/IUStH (@author Disturbing). Insert/Update statement handler. JDBC PreparedStatement -> MySqlCommand.
    /// </summary>
    public interface IUStH
    {
        /// <summary>
        /// Enables coder to manually modify the statement or batch. Must execute the batch or statement manually. Connection is recycled automatically.
        /// </summary>
        void HandleInsertUpdate(MySqlCommand stmt);
    }
}
