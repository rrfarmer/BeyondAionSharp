using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using Aion.Commons.Database;
using Aion.GameServer.Model.GameObjects.Player;

namespace Aion.GameServer.Dao;

/// <summary>
/// Java parity: dao/PlayerAppearanceDAO (@author SoulKeeper, AEJTester, srx47). Loads/stores the 54-field player appearance. MIXED:
/// load via DatabaseFactory, store via the commons DB callback helper (anonymous IUStH->nested StoreHandler capturing id + pa). getInt/
/// getFloat->GetInt32/GetFloat(GetOrdinal); setInt/setFloat->Parameters.Add; execute->ExecuteNonQuery. The Java accessor typo getJawHeigh/
/// setJawHeigh is preserved. Multi-line REPLACE SQL verbatim. On load exception returns null (as Java).
/// </summary>
public class PlayerAppearanceDAO
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(PlayerAppearanceDAO));

    public static PlayerAppearance Load(int playerId)
    {
        PlayerAppearance pa = new PlayerAppearance();
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = "SELECT * FROM player_appearance WHERE player_id = ?";
            stmt.Parameters.Add(new MySqlParameter { Value = playerId });
            using MySqlDataReader resultSet = stmt.ExecuteReader();
            if (resultSet.Read())
            {
                pa.SetFace(resultSet.GetInt32(resultSet.GetOrdinal("face")));
                pa.SetHair(resultSet.GetInt32(resultSet.GetOrdinal("hair")));
                pa.SetDeco(resultSet.GetInt32(resultSet.GetOrdinal("deco")));
                pa.SetTattoo(resultSet.GetInt32(resultSet.GetOrdinal("tattoo")));
                pa.SetFaceContour(resultSet.GetInt32(resultSet.GetOrdinal("face_contour")));
                pa.SetExpression(resultSet.GetInt32(resultSet.GetOrdinal("expression")));
                pa.SetJawLine(resultSet.GetInt32(resultSet.GetOrdinal("jaw_line")));
                pa.SetSkinRGB(resultSet.GetInt32(resultSet.GetOrdinal("skin_rgb")));
                pa.SetHairRGB(resultSet.GetInt32(resultSet.GetOrdinal("hair_rgb")));
                pa.SetEyeRGB(resultSet.GetInt32(resultSet.GetOrdinal("eye_rgb")));
                pa.SetLipRGB(resultSet.GetInt32(resultSet.GetOrdinal("lip_rgb")));
                pa.SetFaceShape(resultSet.GetInt32(resultSet.GetOrdinal("face_shape")));
                pa.SetForehead(resultSet.GetInt32(resultSet.GetOrdinal("forehead")));
                pa.SetEyeHeight(resultSet.GetInt32(resultSet.GetOrdinal("eye_height")));
                pa.SetEyeSpace(resultSet.GetInt32(resultSet.GetOrdinal("eye_space")));
                pa.SetEyeWidth(resultSet.GetInt32(resultSet.GetOrdinal("eye_width")));
                pa.SetEyeSize(resultSet.GetInt32(resultSet.GetOrdinal("eye_size")));
                pa.SetEyeShape(resultSet.GetInt32(resultSet.GetOrdinal("eye_shape")));
                pa.SetEyeAngle(resultSet.GetInt32(resultSet.GetOrdinal("eye_angle")));
                pa.SetBrowHeight(resultSet.GetInt32(resultSet.GetOrdinal("brow_height")));
                pa.SetBrowAngle(resultSet.GetInt32(resultSet.GetOrdinal("brow_angle")));
                pa.SetBrowShape(resultSet.GetInt32(resultSet.GetOrdinal("brow_shape")));
                pa.SetNose(resultSet.GetInt32(resultSet.GetOrdinal("nose")));
                pa.SetNoseBridge(resultSet.GetInt32(resultSet.GetOrdinal("nose_bridge")));
                pa.SetNoseWidth(resultSet.GetInt32(resultSet.GetOrdinal("nose_width")));
                pa.SetNoseTip(resultSet.GetInt32(resultSet.GetOrdinal("nose_tip")));
                pa.SetCheek(resultSet.GetInt32(resultSet.GetOrdinal("cheek")));
                pa.SetLipHeight(resultSet.GetInt32(resultSet.GetOrdinal("lip_height")));
                pa.SetMouthSize(resultSet.GetInt32(resultSet.GetOrdinal("mouth_size")));
                pa.SetLipSize(resultSet.GetInt32(resultSet.GetOrdinal("lip_size")));
                pa.SetSmile(resultSet.GetInt32(resultSet.GetOrdinal("smile")));
                pa.SetLipShape(resultSet.GetInt32(resultSet.GetOrdinal("lip_shape")));
                pa.SetJawHeigh(resultSet.GetInt32(resultSet.GetOrdinal("jaw_height")));
                pa.SetChinJut(resultSet.GetInt32(resultSet.GetOrdinal("chin_jut")));
                pa.SetEarShape(resultSet.GetInt32(resultSet.GetOrdinal("ear_shape")));
                pa.SetHeadSize(resultSet.GetInt32(resultSet.GetOrdinal("head_size")));
                pa.SetNeck(resultSet.GetInt32(resultSet.GetOrdinal("neck")));
                pa.SetNeckLength(resultSet.GetInt32(resultSet.GetOrdinal("neck_length")));
                pa.SetShoulders(resultSet.GetInt32(resultSet.GetOrdinal("shoulders")));
                pa.SetShoulderSize(resultSet.GetInt32(resultSet.GetOrdinal("shoulder_size")));
                pa.SetTorso(resultSet.GetInt32(resultSet.GetOrdinal("torso")));
                pa.SetChest(resultSet.GetInt32(resultSet.GetOrdinal("chest")));
                pa.SetWaist(resultSet.GetInt32(resultSet.GetOrdinal("waist")));
                pa.SetHips(resultSet.GetInt32(resultSet.GetOrdinal("hips")));
                pa.SetArmThickness(resultSet.GetInt32(resultSet.GetOrdinal("arm_thickness")));
                pa.SetArmLength(resultSet.GetInt32(resultSet.GetOrdinal("arm_length")));
                pa.SetHandSize(resultSet.GetInt32(resultSet.GetOrdinal("hand_size")));
                pa.SetLegThickness(resultSet.GetInt32(resultSet.GetOrdinal("leg_thickness")));
                pa.SetLegLength(resultSet.GetInt32(resultSet.GetOrdinal("leg_length")));
                pa.SetFootSize(resultSet.GetInt32(resultSet.GetOrdinal("foot_size")));
                pa.SetFacialRate(resultSet.GetInt32(resultSet.GetOrdinal("facial_rate")));
                pa.SetVoice(resultSet.GetInt32(resultSet.GetOrdinal("voice")));
                pa.SetHeight(resultSet.GetFloat(resultSet.GetOrdinal("height")));
            }
        }
        catch (Exception e)
        {
            log.LogError(e, "Could not restore PlayerAppearance data for player " + playerId + " from DB: " + e.Message);
            return null;
        }
        return pa;
    }

    /// <summary>Saves player appearance in database. Actually calls Store(int, PlayerAppearance).</summary>
    public static bool Store(Player player)
    {
        return Store(player.GetObjectId(), player.GetPlayerAppearance());
    }

    public static bool Store(int id, PlayerAppearance pa)
    {
        return DB.InsertUpdate("REPLACE INTO player_appearance ("
            + "player_id, face, hair, deco, tattoo, face_contour, expression, jaw_line, skin_rgb, hair_rgb, lip_rgb, eye_rgb, face_shape,"
            + "forehead, eye_height, eye_space, eye_width, eye_size, eye_shape, eye_angle,"
            + "brow_height, brow_angle, brow_shape, nose, nose_bridge, nose_width, nose_tip, "
            + "cheek, lip_height, mouth_size, lip_size, smile, lip_shape, jaw_height, chin_jut, ear_shape,"
            + "head_size, neck, neck_length, shoulders, shoulder_size , torso, chest, waist, hips, arm_thickness, arm_length, hand_size,"
            + "leg_thickness, leg_length, foot_size, facial_rate, voice, height)" + " VALUES "
            + "(?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?,"
            + "?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?,?, ?, ?" + ")", new StoreHandler(id, pa));
    }

    private sealed class StoreHandler : IUStH
    {
        private readonly int id;
        private readonly PlayerAppearance pa;

        internal StoreHandler(int id, PlayerAppearance pa)
        {
            this.id = id;
            this.pa = pa;
        }

        public void HandleInsertUpdate(MySqlCommand ps)
        {
            ps.Parameters.Add(new MySqlParameter { Value = id });
            ps.Parameters.Add(new MySqlParameter { Value = pa.GetFace() });
            ps.Parameters.Add(new MySqlParameter { Value = pa.GetHair() });
            ps.Parameters.Add(new MySqlParameter { Value = pa.GetDeco() });
            ps.Parameters.Add(new MySqlParameter { Value = pa.GetTattoo() });
            ps.Parameters.Add(new MySqlParameter { Value = pa.GetFaceContour() });
            ps.Parameters.Add(new MySqlParameter { Value = pa.GetExpression() });
            ps.Parameters.Add(new MySqlParameter { Value = pa.GetJawLine() });
            ps.Parameters.Add(new MySqlParameter { Value = pa.GetSkinRGB() });
            ps.Parameters.Add(new MySqlParameter { Value = pa.GetHairRGB() });
            ps.Parameters.Add(new MySqlParameter { Value = pa.GetLipRGB() });
            ps.Parameters.Add(new MySqlParameter { Value = pa.GetEyeRGB() });
            ps.Parameters.Add(new MySqlParameter { Value = pa.GetFaceShape() });
            ps.Parameters.Add(new MySqlParameter { Value = pa.GetForehead() });
            ps.Parameters.Add(new MySqlParameter { Value = pa.GetEyeHeight() });
            ps.Parameters.Add(new MySqlParameter { Value = pa.GetEyeSpace() });
            ps.Parameters.Add(new MySqlParameter { Value = pa.GetEyeWidth() });
            ps.Parameters.Add(new MySqlParameter { Value = pa.GetEyeSize() });
            ps.Parameters.Add(new MySqlParameter { Value = pa.GetEyeShape() });
            ps.Parameters.Add(new MySqlParameter { Value = pa.GetEyeAngle() });
            ps.Parameters.Add(new MySqlParameter { Value = pa.GetBrowHeight() });
            ps.Parameters.Add(new MySqlParameter { Value = pa.GetBrowAngle() });
            ps.Parameters.Add(new MySqlParameter { Value = pa.GetBrowShape() });
            ps.Parameters.Add(new MySqlParameter { Value = pa.GetNose() });
            ps.Parameters.Add(new MySqlParameter { Value = pa.GetNoseBridge() });
            ps.Parameters.Add(new MySqlParameter { Value = pa.GetNoseWidth() });
            ps.Parameters.Add(new MySqlParameter { Value = pa.GetNoseTip() });
            ps.Parameters.Add(new MySqlParameter { Value = pa.GetCheek() });
            ps.Parameters.Add(new MySqlParameter { Value = pa.GetLipHeight() });
            ps.Parameters.Add(new MySqlParameter { Value = pa.GetMouthSize() });
            ps.Parameters.Add(new MySqlParameter { Value = pa.GetLipSize() });
            ps.Parameters.Add(new MySqlParameter { Value = pa.GetSmile() });
            ps.Parameters.Add(new MySqlParameter { Value = pa.GetLipShape() });
            ps.Parameters.Add(new MySqlParameter { Value = pa.GetJawHeigh() });
            ps.Parameters.Add(new MySqlParameter { Value = pa.GetChinJut() });
            ps.Parameters.Add(new MySqlParameter { Value = pa.GetEarShape() });
            ps.Parameters.Add(new MySqlParameter { Value = pa.GetHeadSize() });
            ps.Parameters.Add(new MySqlParameter { Value = pa.GetNeck() });
            ps.Parameters.Add(new MySqlParameter { Value = pa.GetNeckLength() });
            ps.Parameters.Add(new MySqlParameter { Value = pa.GetShoulders() });
            ps.Parameters.Add(new MySqlParameter { Value = pa.GetShoulderSize() });
            ps.Parameters.Add(new MySqlParameter { Value = pa.GetTorso() });
            ps.Parameters.Add(new MySqlParameter { Value = pa.GetChest() });
            ps.Parameters.Add(new MySqlParameter { Value = pa.GetWaist() });
            ps.Parameters.Add(new MySqlParameter { Value = pa.GetHips() });
            ps.Parameters.Add(new MySqlParameter { Value = pa.GetArmThickness() });
            ps.Parameters.Add(new MySqlParameter { Value = pa.GetArmLength() });
            ps.Parameters.Add(new MySqlParameter { Value = pa.GetHandSize() });
            ps.Parameters.Add(new MySqlParameter { Value = pa.GetLegThickness() });
            ps.Parameters.Add(new MySqlParameter { Value = pa.GetLegLength() });
            ps.Parameters.Add(new MySqlParameter { Value = pa.GetFootSize() });
            ps.Parameters.Add(new MySqlParameter { Value = pa.GetFacialRate() });
            ps.Parameters.Add(new MySqlParameter { Value = pa.GetVoice() });
            ps.Parameters.Add(new MySqlParameter { Value = pa.GetHeight() });
            ps.ExecuteNonQuery();
        }
    }
}
