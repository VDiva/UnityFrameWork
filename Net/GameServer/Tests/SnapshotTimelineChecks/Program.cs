using FrameWork.Script.WebNet;

static void Check(bool condition, string name)
{
    if (!condition) throw new Exception(name);
    Console.WriteLine("PASS: " + name);
}

var timeline = new SnapshotTimeline<double>();
Check(!timeline.TrySample(1.0 / 60, 0.2, out _, out _, out _, out _),
    "empty timeline has no frame");

const double frame = 1.0 / 60;
double previous = double.NegativeInfinity;
double largestStep = 0;
int measured = 0;
bool monotonic = true;
int next = 0;
var arrivals = Enumerable.Range(0, 35)
    .Select(i => (Sent: i * 0.1, Arrival: i * 0.1 + 0.05 +
        (i % 4 == 0 ? 0.025 : i % 4 == 2 ? -0.02 : 0)))
    .ToArray();
for (double now = 0; now < 3.6; now += frame)
{
    while (next < arrivals.Length && arrivals[next].Arrival <= now)
    {
        timeline.Add(arrivals[next].Sent, arrivals[next].Sent * 10, 0.15);
        next++;
    }
    if (!timeline.TrySample(frame, 0.2, out double from, out double to,
            out float alpha, out float extrapolation)) continue;
    double rendered = from + (to - from) * alpha + extrapolation * 10;
    if (now > 0.5 && now < 3.4)
    {
        monotonic &= rendered + 0.0001 >= previous;
        if (!double.IsNegativeInfinity(previous))
            largestStep = Math.Max(largestStep, rendered - previous);
        measured++;
    }
    previous = rendered;
}
Check(monotonic, "jitter playback stays monotonic");
Check(measured > 100 && largestStep < 0.5,
    "jittered 10 Hz snapshots render without large frame jumps");

var gap = new SnapshotTimeline<double>();
gap.Add(1, 10, 0.15);
for (int i = 0; i < 60; i++)
    gap.TrySample(frame, 0.2, out _, out _, out _, out _);
gap.TrySample(frame, 0.2, out double last, out _, out _, out float predicted);
Check(last == 10 && predicted <= 0.2f,
    "missing snapshots have bounded prediction");
gap.Add(5, 50, 0.15);
Check(gap.Count == 1 && gap.TrySample(frame, 0.2, out double reset, out _, out _, out _) &&
      reset == 50, "long pause resets the old timeline");
