using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProcurementHub.Globe
{
    /// <summary>
    /// Renders the Carrix network as a dot-matrix globe into a RenderTexture that the UI displays.
    /// Pins are spend-height columns coloured by business unit; rings pulse on sites with open critical alerts;
    /// arcs show supplier-to-site purchase flows for the selected site.
    /// </summary>
    public sealed class GlobeController : MonoBehaviour
    {
        [SerializeField] Camera globeCamera;
        [SerializeField] Material oceanMaterial;
        [SerializeField] Material atmosphereMaterial;
        [SerializeField] Material landMaterial;
        [SerializeField] Material gridMaterial;
        [SerializeField] Material pinMaterial;
        [SerializeField] Material glowMaterial;

        public static readonly Color PageBackground = new Color32(0x0B, 0x10, 0x19, 0xFF);

        const float R = 1f;

        sealed class Pin
        {
            public Location Location;
            public Vector3 Dir;
            public Transform Column;
            public Renderer ColumnRenderer;
            public Transform Base;
            public Renderer BaseRenderer;
            public Transform AlertRing;
            public Renderer AlertRenderer;
            public float Height;
            public Color UnitColor;
            public Severity? Alert;
            public bool Visible = true;
        }

        sealed class Arc
        {
            public LineRenderer Line;
            public Transform Pulse;
            public Renderer PulseRenderer;
            public Vector3[] Points;
            public float Speed;
            public float Phase;
        }

        readonly List<Pin> pins = new List<Pin>();
        readonly Dictionary<string, Pin> pinById = new Dictionary<string, Pin>();
        readonly List<Arc> arcs = new List<Arc>();
        readonly List<Transform> hubs = new List<Transform>();
        Transform root, selectionRing, hoverRing;
        Renderer selectionRenderer, hoverRenderer;
        MaterialPropertyBlock mpb;
        Mesh columnMesh, ringMesh, dotMesh;

        float yaw = -100f, pitch = 28f, distance = 4.5f;
        float targetYaw = -100f, targetPitch = 28f, targetDistance = 4.5f;
        float idleTime;
        bool focusing;
        string selectedId, hoverId;
        BusinessUnit? unitFilter;
        bool built;

        public bool IsBuilt => built;
        public Camera Camera => globeCamera;

        public static Color UnitColor(BusinessUnit u)
        {
            switch (u)
            {
                case BusinessUnit.SSAMarine: return new Color32(0x39, 0x87, 0xE5, 0xFF);
                case BusinessUnit.RMS: return new Color32(0xD9, 0x59, 0x26, 0xFF);
                case BusinessUnit.Tideworks: return new Color32(0x19, 0x9E, 0x70, 0xFF);
                default: return new Color32(0xC3, 0xC2, 0xB7, 0xFF);
            }
        }

        public static Color SeverityColor(Severity s) =>
            s == Severity.Critical ? (Color)new Color32(0xD0, 0x3B, 0x3B, 0xFF) :
            s == Severity.Serious ? (Color)new Color32(0xEC, 0x83, 0x5A, 0xFF) :
            (Color)new Color32(0xFA, 0xB2, 0x19, 0xFF);

        void Awake()
        {
            mpb = new MaterialPropertyBlock();
            if (globeCamera != null)
            {
                globeCamera.enabled = false;
                globeCamera.clearFlags = CameraClearFlags.SolidColor;
                globeCamera.backgroundColor = PageBackground;
            }
        }

        public void Build(HubDatabase db, Func<Location, double> spendOf, Func<Location, Severity?> alertOf)
        {
            if (built) return;
            built = true;
            root = new GameObject("Globe Geometry").transform;
            root.SetParent(transform, false);

            AddMesh("Ocean", GlobeMeshes.Sphere(R, 96, 48, Color.white), oceanMaterial);
            AddMesh("Graticule", GlobeMeshes.Graticule(R * 1.001f, Color.white), gridMaterial);
            AddMesh("Land", GlobeMeshes.LandDots(26000, R * 1.002f, 0.0072f, Color.white), landMaterial);
            AddMesh("Atmosphere", GlobeMeshes.Sphere(R * 1.13f, 64, 32, Color.white), atmosphereMaterial);

            columnMesh = GlobeMeshes.Column();
            ringMesh = GlobeMeshes.Ring(0.7f, 1f, 40);
            dotMesh = GlobeMeshes.Dot();

            // Spread co-located sites (e.g. five Seattle operations) around a small circle so each stays clickable.
            var groups = new Dictionary<string, List<Location>>();
            foreach (var l in db.Locations)
            {
                string key = Mathf.Round(l.Lat * 1.2f) + "|" + Mathf.Round(l.Lon * 1.2f);
                if (!groups.TryGetValue(key, out var g)) groups[key] = g = new List<Location>();
                g.Add(l);
            }
            foreach (var g in groups.Values)
            {
                for (int i = 0; i < g.Count; i++)
                {
                    var l = g[i];
                    float lat = l.Lat, lon = l.Lon;
                    if (g.Count > 1)
                    {
                        float a = i * Mathf.PI * 2f / g.Count;
                        float rad = 0.55f + 0.12f * g.Count;
                        lat += Mathf.Sin(a) * rad;
                        lon += Mathf.Cos(a) * rad / Mathf.Max(0.3f, Mathf.Cos(l.Lat * Mathf.Deg2Rad));
                    }
                    CreatePin(l, GlobeMeshes.Dir(lat, lon));
                }
            }

            selectionRing = CreateChild("Selection", ringMesh, glowMaterial, out selectionRenderer);
            hoverRing = CreateChild("Hover", ringMesh, glowMaterial, out hoverRenderer);
            selectionRing.gameObject.SetActive(false);
            hoverRing.gameObject.SetActive(false);
            Refresh(spendOf, alertOf);
            ApplyCamera(true);
        }

        void AddMesh(string name, Mesh mesh, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        Transform CreateChild(string name, Mesh mesh, Material mat, out Renderer renderer)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            renderer = mr;
            return go.transform;
        }

        void CreatePin(Location l, Vector3 dir)
        {
            var pin = new Pin { Location = l, Dir = dir, UnitColor = UnitColor(l.Unit) };
            var rot = Quaternion.FromToRotation(Vector3.up, dir);
            pin.Column = CreateChild("Pin " + l.Id, columnMesh, pinMaterial, out pin.ColumnRenderer);
            pin.Column.localPosition = dir * R;
            pin.Column.localRotation = rot;
            pin.Base = CreateChild("Base " + l.Id, ringMesh, glowMaterial, out pin.BaseRenderer);
            pin.Base.localPosition = dir * (R * 1.003f);
            pin.Base.localRotation = rot;
            pin.Base.localScale = Vector3.one * 0.0095f;
            pin.AlertRing = CreateChild("Alert " + l.Id, ringMesh, glowMaterial, out pin.AlertRenderer);
            pin.AlertRing.localPosition = dir * (R * 1.004f);
            pin.AlertRing.localRotation = rot;
            pin.AlertRing.gameObject.SetActive(false);
            pins.Add(pin);
            pinById[l.Id] = pin;
        }

        /// <summary>Re-reads spend and alert state (after filters, resolutions or new requests).</summary>
        public void Refresh(Func<Location, double> spendOf, Func<Location, Severity?> alertOf)
        {
            if (!built) return;
            double min = double.MaxValue, max = 0;
            var spend = new Dictionary<Pin, double>();
            foreach (var p in pins)
            {
                double s = Math.Max(1000, spendOf(p.Location));
                spend[p] = s;
                min = Math.Min(min, s);
                max = Math.Max(max, s);
            }
            double lmin = Math.Log10(min), lmax = Math.Log10(Math.Max(max, min * 10));
            foreach (var p in pins)
            {
                float t = (float)((Math.Log10(spend[p]) - lmin) / (lmax - lmin));
                p.Height = 0.012f + 0.105f * Mathf.Clamp01(t);
                p.Alert = alertOf(p.Location);
                p.Column.localScale = new Vector3(0.0072f, p.Height, 0.0072f);
                p.AlertRing.gameObject.SetActive(p.Alert.HasValue && p.Visible);
            }
            ApplyPinColors();
        }

        public void SetUnitFilter(BusinessUnit? unit)
        {
            unitFilter = unit;
            foreach (var p in pins) p.Visible = !unit.HasValue || p.Location.Unit == unit.Value;
            foreach (var p in pins) p.AlertRing.gameObject.SetActive(p.Alert.HasValue && p.Visible);
            ApplyPinColors();
        }

        void ApplyPinColors()
        {
            foreach (var p in pins)
            {
                var c = p.UnitColor;
                if (!p.Visible) c = Color.Lerp(PageBackground, c, 0.22f);
                else if (p.Location.Id == selectedId) c = Color.Lerp(c, Color.white, 0.35f);
                mpb.SetColor("_BaseColor", c);
                p.ColumnRenderer.SetPropertyBlock(mpb);
                var bc = c; bc.a = p.Visible ? 0.9f : 0.2f;
                mpb.SetColor("_BaseColor", bc);
                p.BaseRenderer.SetPropertyBlock(mpb);
            }
        }

        public void SetSelected(string locationId)
        {
            selectedId = locationId;
            ApplyPinColors();
            if (locationId != null && pinById.TryGetValue(locationId, out var p))
            {
                selectionRing.gameObject.SetActive(true);
                selectionRing.localPosition = p.Dir * (R * 1.005f);
                selectionRing.localRotation = Quaternion.FromToRotation(Vector3.up, p.Dir);
                FocusOn(p.Location.Lat, p.Location.Lon);
            }
            else selectionRing.gameObject.SetActive(false);
        }

        public void SetHover(string locationId)
        {
            hoverId = locationId;
            if (locationId != null && locationId != selectedId && pinById.TryGetValue(locationId, out var p))
            {
                hoverRing.gameObject.SetActive(true);
                hoverRing.localPosition = p.Dir * (R * 1.005f);
                hoverRing.localRotation = Quaternion.FromToRotation(Vector3.up, p.Dir);
                hoverRing.localScale = Vector3.one * 0.022f;
                mpb.SetColor("_BaseColor", new Color(1, 1, 1, 0.55f));
                hoverRenderer.SetPropertyBlock(mpb);
            }
            else hoverRing.gameObject.SetActive(false);
        }

        public void ShowArcs(IList<(float fromLat, float fromLon, string toLocationId, Color color, float weight)> flows)
        {
            ClearArcs();
            foreach (var f in flows)
            {
                if (!pinById.TryGetValue(f.toLocationId, out var target)) continue;
                var a = GlobeMeshes.Dir(f.fromLat, f.fromLon);
                var b = target.Dir;
                float angle = Vector3.Angle(a, b) * Mathf.Deg2Rad;
                if (angle < 0.004f) continue;
                float lift = 0.04f + 0.32f * (angle / Mathf.PI);
                int n = 48;
                var pts = new Vector3[n];
                for (int i = 0; i < n; i++)
                {
                    float t = i / (n - 1f);
                    pts[i] = Vector3.Slerp(a, b, t).normalized * (R + lift * Mathf.Sin(Mathf.PI * t) + 0.004f);
                }
                var go = new GameObject("Arc");
                go.transform.SetParent(root, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.sharedMaterial = glowMaterial;
                lr.useWorldSpace = false;
                lr.positionCount = n;
                lr.SetPositions(pts);
                lr.widthMultiplier = 0.003f + 0.005f * Mathf.Clamp01(f.weight);
                lr.numCapVertices = 2;
                lr.generateLightingData = true;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(f.color, 0), new GradientColorKey(f.color, 1) },
                          new[] { new GradientAlphaKey(0.12f, 0), new GradientAlphaKey(0.9f, 1) });
                lr.colorGradient = g;

                var pulse = CreateChild("Pulse", dotMesh, glowMaterial, out var pr);
                pulse.localScale = Vector3.one * (0.009f + 0.008f * Mathf.Clamp01(f.weight));
                mpb.SetColor("_BaseColor", new Color(f.color.r, f.color.g, f.color.b, 1f));
                pr.SetPropertyBlock(mpb);

                var hub = CreateChild("Hub", ringMesh, glowMaterial, out var hr);
                hub.localPosition = a * (R * 1.003f);
                hub.localRotation = Quaternion.FromToRotation(Vector3.up, a);
                hub.localScale = Vector3.one * 0.014f;
                mpb.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.8f));
                hr.SetPropertyBlock(mpb);
                hubs.Add(hub);

                arcs.Add(new Arc { Line = lr, Pulse = pulse, PulseRenderer = pr, Points = pts, Speed = 0.35f + 0.25f * (1f - angle / Mathf.PI), Phase = UnityEngine.Random.value });
            }
        }

        public void ClearArcs()
        {
            foreach (var a in arcs)
            {
                Destroy(a.Line.gameObject);
                Destroy(a.Pulse.gameObject);
            }
            foreach (var h in hubs) Destroy(h.gameObject);
            arcs.Clear();
            hubs.Clear();
        }

        // ------------------------------------------------------------------ camera & interaction

        public void SetTarget(RenderTexture rt)
        {
            if (globeCamera == null) return;
            globeCamera.targetTexture = rt;
            globeCamera.enabled = rt != null;
        }

        public void SetRendering(bool on)
        {
            if (globeCamera != null) globeCamera.enabled = on && globeCamera.targetTexture != null;
        }

        public void Orbit(Vector2 deltaPanel)
        {
            focusing = false;
            idleTime = 0;
            float k = 0.25f * Mathf.Lerp(0.3f, 1f, Mathf.InverseLerp(1.8f, 6f, distance));
            targetYaw -= deltaPanel.x * k;
            targetPitch = Mathf.Clamp(targetPitch + deltaPanel.y * k, -75f, 80f);
            yaw = targetYaw;
            pitch = targetPitch;
        }

        public void Zoom(float wheelDelta)
        {
            idleTime = 0;
            targetDistance = Mathf.Clamp(targetDistance * (1f + wheelDelta * 0.0015f), 2.1f, 7f);
        }

        public void FocusOn(float lat, float lon)
        {
            focusing = true;
            idleTime = 0;
            targetPitch = Mathf.Clamp(lat, -60f, 70f);
            float delta = Mathf.DeltaAngle(yaw, lon);
            targetYaw = yaw + delta;
            targetDistance = Mathf.Min(targetDistance, 3.4f);
        }

        public void ResetView()
        {
            focusing = true;
            targetYaw = yaw + Mathf.DeltaAngle(yaw, -100f);
            targetPitch = 28f;
            targetDistance = 4.5f;
        }

        void Update()
        {
            if (!built) return;
            float dt = Time.unscaledDeltaTime;
            idleTime += dt;
            if (!focusing && idleTime > 6f && selectedId == null) targetYaw += dt * 2.2f;

            float s = 1f - Mathf.Exp(-dt * 5f);
            yaw = Mathf.Lerp(yaw, targetYaw, s);
            pitch = Mathf.Lerp(pitch, targetPitch, s);
            distance = Mathf.Lerp(distance, targetDistance, s);
            if (focusing && Mathf.Abs(yaw - targetYaw) < 0.05f && Mathf.Abs(pitch - targetPitch) < 0.05f) focusing = false;
            ApplyCamera(false);

            float time = Time.unscaledTime;
            foreach (var p in pins)
            {
                if (!p.Alert.HasValue || !p.AlertRing.gameObject.activeSelf) continue;
                float phase = Mathf.Repeat(time * 0.6f + p.Location.Id.GetHashCode() * 0.001f, 1f);
                p.AlertRing.localScale = Vector3.one * Mathf.Lerp(0.011f, 0.036f, phase);
                var c = SeverityColor(p.Alert.Value);
                c.a = (1f - phase) * 0.9f;
                mpb.SetColor("_BaseColor", c);
                p.AlertRenderer.SetPropertyBlock(mpb);
            }

            if (selectionRing.gameObject.activeSelf)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(time * 3f);
                selectionRing.localScale = Vector3.one * Mathf.Lerp(0.022f, 0.028f, pulse);
                mpb.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.75f + 0.25f * pulse));
                selectionRenderer.SetPropertyBlock(mpb);
            }

            var camRot = globeCamera != null ? globeCamera.transform.rotation : Quaternion.identity;
            foreach (var a in arcs)
            {
                float t = Mathf.Repeat(a.Phase + time * a.Speed, 1f);
                float fi = t * (a.Points.Length - 1);
                int i0 = Mathf.FloorToInt(fi);
                int i1 = Mathf.Min(i0 + 1, a.Points.Length - 1);
                a.Pulse.localPosition = Vector3.Lerp(a.Points[i0], a.Points[i1], fi - i0);
                a.Pulse.rotation = camRot;
            }
        }

        void ApplyCamera(bool snap)
        {
            if (globeCamera == null) return;
            if (snap) { yaw = targetYaw; pitch = targetPitch; distance = targetDistance; }
            var dir = GlobeMeshes.Dir(pitch, yaw);
            globeCamera.transform.position = transform.position + dir * distance;
            globeCamera.transform.LookAt(transform.position, Vector3.up);
        }

        /// <summary>Nearest visible pin to a point in render-texture pixels (origin bottom-left).</summary>
        public string Pick(Vector2 pixel, float radiusPx)
        {
            if (!built || globeCamera == null) return null;
            string best = null;
            float bestD = radiusPx;
            var camPos = globeCamera.transform.position;
            foreach (var p in pins)
            {
                if (!p.Visible) continue;
                var world = transform.TransformPoint(p.Dir * (R + p.Height * 0.6f));
                if (Vector3.Dot(p.Dir, (camPos - world).normalized) < 0.12f) continue;
                var sp = globeCamera.WorldToScreenPoint(world);
                if (sp.z <= 0) continue;
                float d = Vector2.Distance(pixel, new Vector2(sp.x, sp.y));
                if (d < bestD) { bestD = d; best = p.Location.Id; }
            }
            return best;
        }

        /// <summary>Projects a pin tip to render-texture pixels; false when it faces away.</summary>
        public bool TryProject(string locationId, out Vector2 pixel)
        {
            pixel = default;
            if (!built || globeCamera == null || locationId == null || !pinById.TryGetValue(locationId, out var p)) return false;
            var world = transform.TransformPoint(p.Dir * (R + p.Height));
            if (Vector3.Dot(p.Dir, (globeCamera.transform.position - world).normalized) < 0.05f) return false;
            var sp = globeCamera.WorldToScreenPoint(world);
            if (sp.z <= 0) return false;
            pixel = new Vector2(sp.x, sp.y);
            return true;
        }
    }
}
