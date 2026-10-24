using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace PawCollector.Cafe
{
    /// <summary>Paw Café online features over Supabase's plain web API (no SDK):
    /// an invisible guest account (no sign-up), cloud backup of the save, café names you can search, instant friends,
    /// café snapshots for visiting, and the shared Whisker Feed with likes. Everything fails quietly when offline.</summary>
    public class CafeCloud : MonoBehaviour
    {
        public static CafeCloud I { get; private set; }
        // Backend not configured yet (moving to the owner's own Supabase account). While empty, the game stays fully offline.
        const string Url = "https://sakagbeifqcumzbtiivw.supabase.co";
        const string Key = "sb_publishable_THZLMCZsxGPURb3yBViNpg_5UQtjK0r";   // publishable (client) key — safe to ship; RLS protects the data
        static bool Configured => !string.IsNullOrEmpty(Url) && !string.IsNullOrEmpty(Key);
        const string PrefKey = "pawcafe.cloud.v1";

        [Serializable] class Session { public string access_token, refresh_token, user_id; public long expires_at; }
        [Serializable] class AuthUser { public string id; }
        [Serializable] class AuthResp { public string access_token, refresh_token; public long expires_at; public int expires_in; public AuthUser user; public string msg, error_description, message; }

        [Serializable] public class Profile { public string id, cafe_name, avatar_cat; public int level, popularity, followers; }
        [Serializable] public class SnapFurn { public string id, room; public float x, y, z, yaw; }
        [Serializable] public class SnapCat { public string id, name; }
        [Serializable] public class Snapshot { public string user_id; public List<string> rooms = new(); public List<SnapFurn> furniture = new(); public List<SnapCat> cats = new(); }
        [Serializable] public class Post { public string id, user_id, cat, cat_name, pose, image_path, created_at; public int stars, like_count; }
        [Serializable] class Friendship { public string requester, addressee, status; }
        [Serializable] class LikeRow { public string post_id; }
        [Serializable] class Wrap<T> { public List<T> items; }

        Session s;
        public bool Online => s != null && !string.IsNullOrEmpty(s.access_token);
        public string MyId => s?.user_id;
        public Profile Me { get; private set; }
        public string LastError { get; private set; }
        public event Action Changed;
        float syncTimer = 5f;

        public static CafeCloud Ensure()
        {
            if (I) return I;
            var go = new GameObject("CafeCloud"); DontDestroyOnLoad(go);
            return go.AddComponent<CafeCloud>();
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => I = null;

        void Awake()
        {
            I = this;
            try { var j = PlayerPrefs.GetString(PrefKey, ""); if (!string.IsNullOrEmpty(j)) s = JsonUtility.FromJson<Session>(j); } catch { s = null; }
            StartCoroutine(Boot());
        }
        void Update()
        {
            syncTimer -= Time.unscaledDeltaTime;
            if (syncTimer <= 0f) { syncTimer = 90f; if (Online) StartCoroutine(Sync()); else StartCoroutine(Boot()); }
        }
        void OnApplicationPause(bool p) { if (p && Online) StartCoroutine(Sync()); }

        // ============================================================ auth (guest account, no sign-up)
        IEnumerator Boot()
        {
            yield return EnsureSession();
            if (!Online) yield break;
            yield return LoadMe();
            if (Me == null) yield return CreateProfile();
            yield return Sync();
            Changed?.Invoke();
        }
        IEnumerator EnsureSession()
        {
            if (!Configured) yield break;
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (s != null && !string.IsNullOrEmpty(s.access_token) && s.expires_at - 60 > now) yield break;
            AuthResp r = null;
            if (s != null && !string.IsNullOrEmpty(s.refresh_token))
                yield return Auth("/auth/v1/token?grant_type=refresh_token", "{\"refresh_token\":\"" + s.refresh_token + "\"}", x => r = x);
            if (r == null || string.IsNullOrEmpty(r.access_token))
                yield return Auth("/auth/v1/signup", "{\"data\":{}}", x => r = x);   // anonymous guest account
            if (r == null || string.IsNullOrEmpty(r.access_token)) { if (s != null) s.access_token = null; yield break; }
            s = new Session { access_token = r.access_token, refresh_token = r.refresh_token, user_id = r.user?.id ?? s?.user_id,
                              expires_at = r.expires_at > 0 ? r.expires_at : now + Mathf.Max(60, r.expires_in) };
            PlayerPrefs.SetString(PrefKey, JsonUtility.ToJson(s)); PlayerPrefs.Save();
        }
        IEnumerator Auth(string path, string body, Action<AuthResp> done)
        {
            using var req = new UnityWebRequest(Url + path, "POST");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body)); req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("apikey", Key); req.SetRequestHeader("Content-Type", "application/json"); req.timeout = 15;
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success) done(JsonUtility.FromJson<AuthResp>(req.downloadHandler.text));
            else { LastError = req.downloadHandler?.text ?? req.error; Debug.Log("[CafeCloud] auth: " + req.responseCode + " " + LastError); done(null); }
        }

        // ============================================================ REST helper
        public struct Res { public bool ok; public long code; public string text; public byte[] bytes; }
        IEnumerator Call(string method, string path, string body, Action<Res> done, string prefer = null, byte[] raw = null, string contentType = "application/json", bool bytes = false)
        {
            yield return EnsureSession();
            if (!Online) { done?.Invoke(new Res { ok = false, code = 0, text = "offline" }); yield break; }
            using var req = new UnityWebRequest(Url + path, method);
            if (raw != null || body != null) req.uploadHandler = new UploadHandlerRaw(raw ?? Encoding.UTF8.GetBytes(body));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("apikey", Key); req.SetRequestHeader("Authorization", "Bearer " + s.access_token);
            if (raw != null || body != null) req.SetRequestHeader("Content-Type", contentType);
            if (prefer != null) req.SetRequestHeader("Prefer", prefer);
            req.timeout = 20;
            yield return req.SendWebRequest();
            var res = new Res { ok = req.result == UnityWebRequest.Result.Success, code = req.responseCode, text = req.downloadHandler?.text, bytes = bytes ? req.downloadHandler?.data : null };
            if (!res.ok) { LastError = res.text ?? req.error; Debug.Log($"[CafeCloud] {method} {path} → {req.responseCode} {LastError}"); }
            done?.Invoke(res);
        }
        static List<T> Arr<T>(string json) { try { return JsonUtility.FromJson<Wrap<T>>("{\"items\":" + (string.IsNullOrEmpty(json) ? "[]" : json) + "}")?.items ?? new List<T>(); } catch { return new List<T>(); } }
        static string Esc(string v) => v == null ? "null" : "\"" + v.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

        // ============================================================ profile & café name
        IEnumerator LoadMe()
        {
            List<Profile> rows = null;
            yield return Call("GET", "/rest/v1/profiles?id=eq." + s.user_id + "&select=*", null, r => { if (r.ok) rows = Arr<Profile>(r.text); });
            Me = rows?.FirstOrDefault();
        }
        IEnumerator CreateProfile()
        {
            var d = CafeManager.I ? CafeManager.I.Data : null;
            string cat = d?.residents.FirstOrDefault() ?? "Mochi";
            for (int i = 0; i < 6 && Me == null; i++)
            {
                string name = $"{(d != null ? d.Name(cat) : cat)} Cafe {UnityEngine.Random.Range(100, 9999)}";
                if (!ValidName(name)) name = $"Paw Cafe {UnityEngine.Random.Range(1000, 99999)}";
                bool ok = false;
                yield return Call("POST", "/rest/v1/profiles", $"{{\"id\":\"{s.user_id}\",\"cafe_name\":{Esc(name)},\"avatar_cat\":{Esc(cat)}}}", r => ok = r.ok, "return=minimal");
                if (ok) yield return LoadMe();
            }
        }
        public static bool ValidName(string n) => !string.IsNullOrEmpty(n) && n.Trim().Length >= 3 && n.Trim().Length <= 20 && n.All(c => char.IsLetterOrDigit(c) && c < 128 || c == ' ' || c == '_' || c == '\'' || c == '.' || c == '-');
        /// <summary>Rename your café. done(null) = ok, else a short reason.</summary>
        public void Rename(string name, Action<string> done) => StartCoroutine(RenameCo(name.Trim(), done));
        IEnumerator RenameCo(string name, Action<string> done)
        {
            if (!ValidName(name)) { done("Use 3–20 letters, numbers or spaces"); yield break; }
            Res res = default;
            yield return Call("PATCH", "/rest/v1/profiles?id=eq." + s?.user_id, $"{{\"cafe_name\":{Esc(name)},\"updated_at\":\"now()\"}}".Replace("\"now()\"", "\"" + DateTime.UtcNow.ToString("o") + "\""), r => res = r, "return=minimal");
            if (res.ok) { yield return LoadMe(); Changed?.Invoke(); done(null); }
            else done(res.code == 0 ? "You're offline" : res.text != null && res.text.Contains("23505") ? "That name is taken" : "Couldn't rename right now");
        }

        // ============================================================ sync: profile stats, café snapshot, cloud save
        public IEnumerator Sync()
        {
            var cm = CafeManager.I; if (!cm || !Online) yield break;
            var d = cm.Data;
            string now = DateTime.UtcNow.ToString("o");
            string avatar = d.residents.FirstOrDefault() ?? "Mochi";
            yield return Call("PATCH", "/rest/v1/profiles?id=eq." + s.user_id,
                $"{{\"level\":{d.level},\"popularity\":{Mathf.RoundToInt(cm.Popularity)},\"followers\":{d.followers},\"avatar_cat\":{Esc(avatar)},\"updated_at\":\"{now}\"}}", null, "return=minimal");
            var snap = new Snapshot { user_id = s.user_id, rooms = d.rooms.ToList() };
            foreach (var p in d.furniture) snap.furniture.Add(new SnapFurn { id = p.id, room = p.room, x = p.x, y = p.y, z = p.z, yaw = p.yaw });
            foreach (var id in d.residents) snap.cats.Add(new SnapCat { id = id, name = d.Name(id) });
            string snapJson = $"{{\"user_id\":\"{s.user_id}\",\"rooms\":{ListJson(snap.rooms)},\"furniture\":{ArrJson(snap.furniture)},\"cats\":{ArrJson(snap.cats)},\"updated_at\":\"{now}\"}}";
            yield return Call("POST", "/rest/v1/cafe_snapshots", snapJson, null, "resolution=merge-duplicates,return=minimal");
            string save = JsonUtility.ToJson(d);
            yield return Call("POST", "/rest/v1/saves", $"{{\"user_id\":\"{s.user_id}\",\"data\":{save},\"version\":{d.version},\"updated_at\":\"{now}\"}}", null, "resolution=merge-duplicates,return=minimal");
        }
        static string ListJson(List<string> l) => "[" + string.Join(",", l.Select(Esc)) + "]";
        static string ArrJson<T>(List<T> l) => "[" + string.Join(",", l.Select(x => JsonUtility.ToJson(x))) + "]";

        // ============================================================ friends (instant — no requests to wait on)
        public void Search(string q, Action<List<Profile>> done) => StartCoroutine(Call("POST", "/rest/v1/rpc/search_cafes", "{\"q\":" + Esc(q) + "}", r => done(r.ok ? Arr<Profile>(r.text) : null)));
        public void Friends(Action<List<Profile>> done) => StartCoroutine(FriendsCo(done));
        IEnumerator FriendsCo(Action<List<Profile>> done)
        {
            List<Friendship> rows = null;
            yield return Call("GET", "/rest/v1/friendships?select=requester,addressee,status", null, r => { if (r.ok) rows = Arr<Friendship>(r.text); });
            if (rows == null) { done(null); yield break; }
            var ids = rows.Select(f => f.requester == s.user_id ? f.addressee : f.requester).Distinct().ToList();
            if (ids.Count == 0) { done(new List<Profile>()); yield break; }
            List<Profile> profs = null;
            yield return Call("GET", "/rest/v1/profiles?select=*&id=in.(" + string.Join(",", ids) + ")", null, r => { if (r.ok) profs = Arr<Profile>(r.text); });
            done(profs?.OrderByDescending(p => p.level).ToList());
        }
        public void AddFriend(string id, Action<bool> done) =>
            StartCoroutine(Call("POST", "/rest/v1/friendships", $"{{\"requester\":\"{s?.user_id}\",\"addressee\":\"{id}\",\"status\":\"accepted\"}}", r => done(r.ok || r.code == 409), "return=minimal"));
        public void RemoveFriend(string id, Action<bool> done) =>
            StartCoroutine(Call("DELETE", $"/rest/v1/friendships?or=(and(requester.eq.{s?.user_id},addressee.eq.{id}),and(requester.eq.{id},addressee.eq.{s?.user_id}))", null, r => done(r.ok)));
        public void LoadSnapshot(string id, Action<Snapshot> done) => StartCoroutine(Call("GET", "/rest/v1/cafe_snapshots?user_id=eq." + id + "&select=*", null, r => done(r.ok ? Arr<Snapshot>(r.text).FirstOrDefault() : null)));

        // ============================================================ Whisker Feed
        public void PostPhoto(Texture2D tex, string cat, string catName, string pose, int stars) => StartCoroutine(PostCo(tex, cat, catName, pose, stars));
        IEnumerator PostCo(Texture2D tex, string cat, string catName, string pose, int stars)
        {
            yield return EnsureSession(); if (!Online || tex == null) yield break;
            byte[] jpg; try { jpg = tex.EncodeToJPG(82); } catch { yield break; }
            string path = $"{s.user_id}/{Guid.NewGuid():N}.jpg"; bool up = false;
            yield return Call("POST", "/storage/v1/object/feed/" + path, null, r => up = r.ok, null, jpg, "image/jpeg");
            if (!up) yield break;
            yield return Call("POST", "/rest/v1/feed_posts", $"{{\"user_id\":\"{s.user_id}\",\"cat\":{Esc(cat)},\"cat_name\":{Esc(catName)},\"pose\":{Esc(pose)},\"stars\":{Mathf.Clamp(stars, 0, 3)},\"image_path\":{Esc(path)}}}", null, "return=minimal");
            Changed?.Invoke();
        }
        public void Feed(Action<List<Post>, HashSet<string>> done) => StartCoroutine(FeedCo(done));
        IEnumerator FeedCo(Action<List<Post>, HashSet<string>> done)
        {
            List<Post> posts = null; var liked = new HashSet<string>();
            yield return Call("GET", "/rest/v1/feed_posts?select=*&order=created_at.desc&limit=40", null, r => { if (r.ok) posts = Arr<Post>(r.text); });
            if (posts == null) { done(null, liked); yield break; }
            yield return Call("GET", "/rest/v1/feed_likes?select=post_id&user_id=eq." + s.user_id, null, r => { if (r.ok) foreach (var l in Arr<LikeRow>(r.text)) liked.Add(l.post_id); });
            done(posts, liked);
        }
        public void Like(string postId, bool on, Action<bool> done) => StartCoroutine(on
            ? Call("POST", "/rest/v1/feed_likes", $"{{\"post_id\":\"{postId}\",\"user_id\":\"{s?.user_id}\"}}", r => done(r.ok || r.code == 409), "return=minimal")
            : Call("DELETE", $"/rest/v1/feed_likes?post_id=eq.{postId}&user_id=eq.{s?.user_id}", null, r => done(r.ok)));

        readonly Dictionary<string, Texture2D> imgCache = new();
        public void Image(string path, Action<Texture2D> done)
        {
            if (imgCache.TryGetValue(path, out var t) && t) { done(t); return; }
            StartCoroutine(Call("GET", "/storage/v1/object/authenticated/feed/" + path, null, r =>
            {
                if (!r.ok || r.bytes == null) { done(null); return; }
                var tex = new Texture2D(2, 2); if (tex.LoadImage(r.bytes)) { imgCache[path] = tex; done(tex); } else done(null);
            }, bytes: true));
        }

        // names for posts by other players
        readonly Dictionary<string, string> names = new();
        public string NameOf(string userId) => userId == MyId ? (Me?.cafe_name ?? "You") : names.TryGetValue(userId, out var n) ? n : "A friend";
        public void Remember(IEnumerable<Profile> ps) { if (ps != null) foreach (var p in ps) names[p.id] = p.cafe_name; }
    }
}
