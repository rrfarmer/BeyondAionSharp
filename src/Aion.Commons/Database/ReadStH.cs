using MySqlConnector;

namespace Aion.Commons.Database
{
    /// <summary>
    /// Java parity: commons/database/ReadStH (@author Disturbing). Read statement handler. JDBC ResultSet -> MySqlDataReader.
    /// </summary>
    public interface ReadStH
    {
        /// <summary>
        /// Allows coder to read data after query execution. Automatically recycles connection and closes the reader.
        /// </summary>
        void HandleRead(MySqlDataReader rset);
    }
}
