namespace HistoryExportCmd;

internal class Point
{
	public int PointId { get; set; }

	public string PointName { get; set; }

	public string ParamName { get; set; }

	public string Descriptor { get; set; }

	public string Device { get; set; }

	public bool HistoryFast { get; set; }

	public bool HistorySlow { get; set; }

	public bool HistoryExtd { get; set; }

	public bool HistoryFastArch { get; set; }

	public bool HistorySlowArch { get; set; }

	public bool HistoryExtdArch { get; set; }
}
