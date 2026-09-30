"""Send a C# snippet to the local UnityMCP execute_code tool. usage: python mcp_exec.py <file.cs>"""
import json, sys, urllib.request

URL = "http://127.0.0.1:8080/mcp"
SID_FILE = "D:/PCUBE/UnderCooked/results/.mcp_sid"
HDR = {"Content-Type": "application/json; charset=utf-8", "Accept": "application/json, text/event-stream"}


def post(body, sid=None):
    h = dict(HDR)
    if sid:
        h["mcp-session-id"] = sid
    req = urllib.request.Request(URL, data=json.dumps(body).encode("utf-8"), headers=h, method="POST")
    with urllib.request.urlopen(req, timeout=120) as r:
        return r.headers.get("mcp-session-id"), r.read().decode("utf-8")


def session():
    try:
        return open(SID_FILE).read().strip()
    except OSError:
        pass
    sid, _ = post({"jsonrpc": "2.0", "id": 1, "method": "initialize",
                   "params": {"protocolVersion": "2025-03-26", "capabilities": {},
                              "clientInfo": {"name": "claude-direct", "version": "1"}}})
    post({"jsonrpc": "2.0", "method": "notifications/initialized"}, sid)
    open(SID_FILE, "w").write(sid)
    return sid


code = open(sys.argv[1], encoding="utf-8").read()
body = {"jsonrpc": "2.0", "id": 2, "method": "tools/call",
        "params": {"name": "execute_code", "arguments": {"action": "execute", "code": code}}}
_, text = post(body, session())
if "Session not found" in text:
    open(SID_FILE, "w").close()
    import os; os.remove(SID_FILE)
    _, text = post(body, session())
sys.stdout.reconfigure(encoding="utf-8")
for line in text.splitlines():
    if line.startswith("data: "):
        d = json.loads(line[6:])
        if "result" in d:
            for c in d["result"].get("content", []):
                print(c.get("text", ""))
        else:
            print(json.dumps(d, ensure_ascii=False))
