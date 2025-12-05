using System;
using System.Data;
using System.Data.SqlClient;

namespace HistoryExport;

internal class DBAccess
{
	private string mConnectionString;

	public DBAccess(string cn)
	{
		mConnectionString = cn;
	}

	public bool GetParameter(string name, out bool value, out string errtext)
	{
		bool res = false;
		errtext = null;
		value = false;
		try
		{
			using SqlConnection con = new SqlConnection(mConnectionString);
			con.Open();
			using SqlCommand cmd = con.CreateCommand();
			cmd.CommandText = "SELECT Value FROM Parameter WHERE Name = @Name";
			cmd.Parameters.AddWithValue("@Name", name);
			using SqlDataReader dr = cmd.ExecuteReader();
			if (dr.Read() && !dr.IsDBNull(0))
			{
				value = Convert.ToInt32(dr.GetString(0)) != 0;
				res = true;
			}
		}
		catch (Exception ex)
		{
			errtext = ex.Message;
		}
		return res;
	}

	public bool GetPoints(out Points points, out string errtext)
	{
		bool res = false;
		points = null;
		errtext = null;
		try
		{
			using SqlConnection con = new SqlConnection(mConnectionString);
			con.Open();
			using SqlCommand cmd = con.CreateCommand();
			cmd.CommandText = "SELECT PointId, PointName, ParamName, Description, Device, HistoryFast, HistorySlow, HistoryExtd, HistoryFastArch, HistorySlowArch, HistoryExtdArch FROM Point ORDER BY PointName";
			cmd.ExecuteNonQuery();
			using SqlDataReader dr = cmd.ExecuteReader();
			points = new Points();
			while (dr.Read())
			{
				int n = 0;
				Point point = new Point();
				point.PointId = dr.GetInt32(n);
				n++;
				point.PointName = dr.GetString(n);
				n++;
				point.ParamName = dr.GetString(n);
				n++;
				point.Descriptor = (dr.IsDBNull(n) ? null : dr.GetString(n));
				n++;
				point.Device = (dr.IsDBNull(n) ? null : dr.GetString(n));
				n++;
				point.HistoryFast = !dr.IsDBNull(n) && dr.GetBoolean(n);
				n++;
				point.HistorySlow = !dr.IsDBNull(n) && dr.GetBoolean(n);
				n++;
				point.HistoryExtd = !dr.IsDBNull(n) && dr.GetBoolean(n);
				n++;
				point.HistoryFastArch = !dr.IsDBNull(n) && dr.GetBoolean(n);
				n++;
				point.HistorySlowArch = !dr.IsDBNull(n) && dr.GetBoolean(n);
				n++;
				point.HistoryExtdArch = !dr.IsDBNull(n) && dr.GetBoolean(n);
				n++;
				points.Add(point);
			}
			res = true;
		}
		catch (Exception ex)
		{
			errtext = ex.Message;
		}
		return res;
	}

	public bool Refresh(string path, out string errtext)
	{
		bool res = false;
		errtext = null;
		try
		{
			using SqlConnection con = new SqlConnection(mConnectionString);
			con.Open();
			using SqlCommand cmd = con.CreateCommand();
			cmd.CommandType = CommandType.StoredProcedure;
			cmd.CommandText = "sp_ReadEbiPoints";
			cmd.Parameters.Add("@ProcessPnt", SqlDbType.NVarChar).Value = path;
			cmd.Parameters.Add("@retval", SqlDbType.Int).Direction = ParameterDirection.ReturnValue;
			cmd.ExecuteNonQuery();
			int? retval = ((cmd.Parameters["@retval"].Value is DBNull) ? ((int?)null) : ((int?)cmd.Parameters["@retval"].Value));
			if (retval.HasValue && retval.Value == 0)
			{
				res = true;
			}
			else
			{
				errtext = "Failure executing the stored procedure";
			}
		}
		catch (Exception ex)
		{
			errtext = ex.Message;
		}
		return res;
	}

	public bool StoreParameter(string name, bool value, out string errtext)
	{
		bool res = false;
		errtext = null;
		try
		{
			using SqlConnection con = new SqlConnection(mConnectionString);
			con.Open();
			using SqlCommand cmd = con.CreateCommand();
			cmd.CommandText = "UPDATE Parameter SET Value = @Value WHERE Name = @Name";
			cmd.Parameters.AddWithValue("@Name", name);
			cmd.Parameters.AddWithValue("@Value", value ? "1" : "0");
			cmd.ExecuteNonQuery();
			res = true;
		}
		catch (Exception ex)
		{
			errtext = ex.Message;
		}
		return res;
	}

	public bool UpdatePoint(int PointId, string descriptor, string device, bool historyFastArch, bool historySlowArch, bool historyExtdArch, out string errtext)
	{
		bool res = false;
		errtext = null;
		try
		{
			using SqlConnection con = new SqlConnection(mConnectionString);
			con.Open();
			using SqlCommand cmd = con.CreateCommand();
			cmd.CommandText = "UPDATE Point SET Description = @Descriptor, Device = @Device, HistoryFastArch = @HistoryFastArch, HistorySlowArch = @HistorySlowArch, HistoryExtdArch = @HistoryExtdArch  WHERE PointId = @PointId ";
			cmd.Parameters.AddWithValue("@PointId", PointId);
			cmd.Parameters.AddWithValue("@Descriptor", descriptor);
			cmd.Parameters.AddWithValue("@Device", device);
			cmd.Parameters.AddWithValue("@HistoryFastArch", historyFastArch);
			cmd.Parameters.AddWithValue("@HistorySlowArch", historySlowArch);
			cmd.Parameters.AddWithValue("@HistoryExtdArch", historyExtdArch);
			cmd.ExecuteNonQuery();
			res = true;
		}
		catch (Exception ex)
		{
			errtext = ex.Message;
		}
		return res;
	}
}
