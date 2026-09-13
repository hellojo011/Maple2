using System.Numerics;
using DotRecast.Core.Numerics;
using DotRecast.Detour;
using DotRecast.Detour.Crowd;
using DotRecast.Detour.Io;
using DotRecast.Recast.Toolset;
using DotRecast.Recast.Toolset.Builder;
using Maple2.Model.Metadata;
using Maple2.Server.Game.Model;
using Maple2.Tools.DotRecast;
using Serilog;

namespace Maple2.Server.Game.Manager.Field;

public sealed class Navigation : IDisposable {
    private static readonly ILogger Logger = Log.Logger.ForContext<Navigation>();

    public readonly string Name;
    private readonly DtNavMesh navMesh;
    private readonly DtNavMeshQuery navMeshQuery;
    public DtCrowd Crowd { get; private set; }
    private readonly DtCrowdAgentConfig crowdAgentConfig = new DtCrowdAgentConfig();

    // MS2TriggerAgent is a one-block path blocker that trigger scripts switch on and off - three of them
    // close the bridge in 52000120_qd while it is raised. The navmesh is prebaked, so a live agent disables
    // the polygons under it instead; the crowd's query filter already excludes disabled polygons.
    private static readonly RcVec3f AgentHalfExtents = new(0.75f, 1.5f, 0.75f);
    private readonly HashSet<int> blockingAgents = [];
    private readonly Dictionary<long, int> blockedPolys = [];

    public Navigation(string name) {
        Name = name;
        navMesh = LoadNavMesh();
        navMeshQuery = new DtNavMeshQuery(navMesh);

        Crowd = new DtCrowd(new DtCrowdConfig(maxAgentRadius: 0.3f), navMesh, __ => new DtQueryDefaultFilter(
            SampleAreaModifications.SAMPLE_POLYFLAGS_ALL,
            SampleAreaModifications.SAMPLE_POLYFLAGS_DISABLED,
            [1f, 10f, 1f, 1f, 2f, 1.5f]) // TODO: understand what actually these values are
        );
    }

    private DtNavMesh LoadNavMesh() {
        FileStream fs = new FileStream(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Navmeshes", $"{Name}.navmesh"), FileMode.Open, FileAccess.Read);
        BinaryReader br = new BinaryReader(fs);
        DtMeshSetReader reader = new DtMeshSetReader();

        DtNavMesh dtNavMesh = reader.Read(br, DotRecastHelper.VERTS_PER_POLY);
        br.Close();
        fs.Close();
        return dtNavMesh;
    }

    public AgentNavigation ForAgent(FieldNpc npc, DtCrowdAgent agent) {
        return new AgentNavigation(npc, agent, Crowd);
    }

    public DtCrowdAgent AddAgent(NpcMetadata metadata, Vector3 origin) {
        RcNavMeshBuildSettings settings = DotRecastHelper.NavMeshBuildSettings;
        // use metadata speed instead of settings?
        DtCrowdAgentParams agentParams = CreateAgentParams(0.3f, 1.4f, settings.agentMaxAcceleration, settings.agentMaxSpeed);
        return Crowd.AddAgent(DotRecastHelper.ToNavMeshSpace(origin), agentParams);
    }

    private DtCrowdAgentParams CreateAgentParams(float agentRadius, float agentHeight, float agentMaxAcceleration, float agentMaxSpeed) {
        DtCrowdAgentParams ap = new() {
            radius = agentRadius,
            height = agentHeight,
            maxAcceleration = agentMaxAcceleration,
            maxSpeed = agentMaxSpeed,
            updateFlags = crowdAgentConfig.GetUpdateFlags(),
            obstacleAvoidanceType = crowdAgentConfig.obstacleAvoidanceType,
            separationWeight = crowdAgentConfig.separationWeight,
        };
        ap.collisionQueryRange = ap.radius * 12.0f;
        ap.pathOptimizationRange = ap.radius * 30.0f;
        return ap;
    }

    public void SetAgentBlocking(int triggerId, Vector3 position, bool blocking) {
        if (blocking ? !blockingAgents.Add(triggerId) : !blockingAgents.Remove(triggerId)) {
            return;
        }

        // Match every polygon, including ones already disabled, so switching an agent off finds the same set.
        var filter = new DtQueryDefaultFilter(SampleAreaModifications.SAMPLE_POLYFLAGS_ALL, 0, [1f, 10f, 1f, 1f, 2f, 1.5f]);
        var polys = new List<long>();
        navMeshQuery.QueryPolygons(DotRecastHelper.ToNavMeshSpace(position), AgentHalfExtents, filter, new PolyRefCollector(polys));

        foreach (long polyRef in polys) {
            // Neighbouring agents overlap the same polygons, so only reopen one when no agent covers it.
            int count = blockedPolys.GetValueOrDefault(polyRef) + (blocking ? 1 : -1);
            if (count > 0) {
                blockedPolys[polyRef] = count;
            } else {
                blockedPolys.Remove(polyRef);
            }

            navMesh.GetPolyFlags(polyRef, out int flags);
            navMesh.SetPolyFlags(polyRef, count > 0
                ? flags | SampleAreaModifications.SAMPLE_POLYFLAGS_DISABLED
                : flags & ~SampleAreaModifications.SAMPLE_POLYFLAGS_DISABLED);
        }
    }

    public void Dispose() {

    }

    private sealed class PolyRefCollector(List<long> polys) : IDtPolyQuery {
        public void Process(DtMeshTile tile, DtPoly[] polyArray, Span<long> refs, int count) {
            for (int i = 0; i < count; i++) {
                polys.Add(refs[i]);
            }
        }
    }
}
