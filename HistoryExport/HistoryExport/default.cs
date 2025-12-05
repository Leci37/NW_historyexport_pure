using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Web;
using System.Web.UI;
using System.Web.UI.HtmlControls;
using System.Web.UI.WebControls;
using Microsoft.Reporting.WebForms;

namespace HistoryExport;

public class @default : Page
{
	private DBAccess mDB;

	private LogFile mLogfile;

	protected HtmlForm form1;

	protected Label lbError;

	protected Button btExport;

	protected DropDownList ddExport;

	protected Button btRefreshFromEBI;

	protected Button btSave;

	protected GridView grid;

	protected void Page_Load(object sender, EventArgs e)
	{
		NameValueCollection config = ConfigurationManager.AppSettings;
		LogFile.SetConfig(typeof(LogFlags), config);
		mLogfile = new LogFile("HistoryExport_#.log");
		string cn = ConfigurationManager.ConnectionStrings["PointsHistory"].ConnectionString;
		mDB = new DBAccess(cn);
		lbError.Text = null;
		if (base.IsPostBack)
		{
			return;
		}
		Points points;
		string errtext;
		bool res = mDB.GetPoints(out points, out errtext);
		if (res)
		{
			if (points.Count == 0)
			{
				string processPnt = ConfigurationManager.AppSettings["ProcessPnt"];
				res = RefreshPoints(processPnt, out errtext);
				if (res)
				{
					res = mDB.GetPoints(out points, out errtext);
					if (!res)
					{
						lbError.Text = errtext;
					}
				}
				else
				{
					lbError.Text = errtext;
				}
			}
			if (res)
			{
				grid.DataSource = points;
				grid.DataBind();
				Session["Points"] = points;
			}
			ddExport.Items.Add(new ListItem("Excel", "1"));
			ddExport.Items.Add(new ListItem("Pdf", "2"));
		}
		else
		{
			lbError.Text = errtext;
		}
	}

	protected void btExport_Click(object sender, EventArgs e)
	{
		string selectedValue = ddExport.SelectedValue;
		string type = null;
		string ext = null;
		if (selectedValue == "1")
		{
			ext = "xlsx";
			type = "EXCELOPENXML";
		}
		if (selectedValue == "2")
		{
			ext = "pdf";
			type = "PDF";
		}
		string deviceInfo = null;
		LocalReport localReport = new LocalReport();
		localReport.ListRenderingExtensions();
		localReport.ReportPath = "Points.rdlc";
		List<ReportParameter> parms = new List<ReportParameter>
		{
			new ReportParameter("ServerName", HttpContext.Current.Server.MachineName)
		};
		localReport.DataSources.Add(new ReportDataSource("Points", Session["Points"]));
		localReport.SetParameters(parms);
		localReport.Refresh();
		string mimeType;
		string encoding;
		string extension;
		string[] streamids;
		Warning[] warnings;
		byte[] bytes = localReport.Render(type, deviceInfo, out mimeType, out encoding, out extension, out streamids, out warnings);
		string filename = $"tmp/Points-{DateTime.Now:yyyyMMddHHmmss}.{ext}";
		string filepath = base.Server.MapPath(filename);
		Directory.CreateDirectory(Path.GetDirectoryName(filepath));
		if (File.Exists(filepath))
		{
			File.Delete(filepath);
		}
		using (FileStream fs = new FileStream(filepath, FileMode.Create))
		{
			fs.Write(bytes, 0, bytes.Length);
			fs.Close();
		}
		ScriptManager.RegisterStartupScript(this, GetType(), "redirecting", "window.location ='" + filename + "';", addScriptTags: true);
	}

	protected void btRefresh_Click(object sender, EventArgs e)
	{
		string processPnt = ConfigurationManager.AppSettings["ProcessPnt"];
		if (RefreshPoints(processPnt, out var errtext))
		{
			base.Response.Redirect("default.aspx");
		}
		else
		{
			lbError.Text = errtext;
		}
	}

	protected void btRefresh2_Click(object sender, EventArgs e)
	{
		string processPnt = ConfigurationManager.AppSettings["ProcessPnt"];
		if (mDB.Refresh(processPnt, out var errtext))
		{
			base.Response.Redirect("default.aspx");
		}
		else
		{
			lbError.Text = errtext;
		}
	}

	protected void btSave_Click(object sender, EventArgs e)
	{
		bool res = true;
		string errtext = null;
		Points points = Session["Points"] as Points;
		if (points == null)
		{
			res = mDB.GetPoints(out points, out errtext);
		}
		if (res)
		{
			foreach (GridViewRow row in grid.Rows)
			{
				int pointId = Convert.ToInt32(grid.DataKeys[row.RowIndex].Value);
				Point point = points.Find((Point p) => p.PointId == pointId);
				string descriptor = (row.FindControl("Descriptor") as TextBox).Text;
				string device = (row.FindControl("Device") as TextBox).Text;
				bool historyFastArch = (row.FindControl("HistoryFastArch") as CheckBox).Checked;
				bool historySlowArch = (row.FindControl("HistorySlowArch") as CheckBox).Checked;
				bool historyExtdArch = (row.FindControl("HistoryExtdArch") as CheckBox).Checked;
				if (point.Descriptor != descriptor || point.Device != device || point.HistoryFastArch != historyFastArch || point.HistorySlowArch != historySlowArch || point.HistoryExtdArch != historyExtdArch)
				{
					res = mDB.UpdatePoint(point.PointId, descriptor, device, historyFastArch, historySlowArch, historyExtdArch, out errtext);
					if (!res)
					{
						lbError.Text = errtext;
						break;
					}
					lbError.Text = "OK";
				}
			}
			if (res)
			{
				base.Response.Redirect("default.aspx");
			}
		}
		else
		{
			lbError.Text = errtext;
		}
	}

	protected void grid_Prerender(object sender, EventArgs e)
	{
		GridView grid = sender as GridView;
		if (grid.Rows.Count > 0)
		{
			grid.HeaderRow.TableSection = TableRowSection.TableHeader;
			if (grid.BottomPagerRow != null)
			{
				grid.BottomPagerRow.TableSection = TableRowSection.TableFooter;
			}
		}
	}

	protected void grid_RowDataBound(object sender, GridViewRowEventArgs e)
	{
		if (e.Row.RowType == DataControlRowType.DataRow)
		{
			GridViewRow row = e.Row;
			Point point = row.DataItem as Point;
			(row.FindControl("Descriptor") as TextBox).Text = point.Descriptor;
			(row.FindControl("Device") as TextBox).Text = point.Device;
			CheckBox obj = row.FindControl("HistoryFastArch") as CheckBox;
			obj.Visible = point.HistoryFast;
			obj.Checked = point.HistoryFastArch;
			CheckBox obj2 = row.FindControl("HistorySlowArch") as CheckBox;
			obj2.Visible = point.HistorySlow;
			obj2.Checked = point.HistorySlowArch;
			CheckBox obj3 = row.FindControl("HistoryExtdArch") as CheckBox;
			obj3.Visible = point.HistoryExtd;
			obj3.Checked = point.HistoryExtdArch;
		}
	}

	private bool RefreshPoints(string processPnt, out string errtext)
	{
		bool res = false;
		errtext = null;
		try
		{
			using Process myProcess1 = new Process();
			string file = Path.Combine(Path.GetTempPath(), "allpoints.pnt");
			_ = "-out " + file;
			myProcess1.StartInfo.UseShellExecute = false;
			myProcess1.StartInfo.FileName = "bckbld.exe";
			myProcess1.StartInfo.Arguments = "-out " + file;
			myProcess1.StartInfo.CreateNoWindow = true;
			myProcess1.Start();
			myProcess1.WaitForExit();
			if (myProcess1.ExitCode == 0)
			{
				using Process myProcess2 = new Process();
				myProcess2.StartInfo.UseShellExecute = false;
				myProcess2.StartInfo.FileName = processPnt;
				myProcess2.StartInfo.Arguments = file;
				myProcess2.StartInfo.CreateNoWindow = true;
				myProcess2.Start();
				myProcess2.WaitForExit();
				int retval = myProcess2.ExitCode;
				if (retval == 0)
				{
					res = true;
				}
				else
				{
					errtext = $"Failure executing ProcessPoints: {retval}";
				}
			}
			else
			{
				errtext = "Failure executing bckbld";
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine(ex.Message);
			errtext = ex.Message;
			res = false;
		}
		return res;
	}
}
