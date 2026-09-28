#!/bin/bash
# usage: results/.tools/mcp.sh <tool_name> '<json args>'
# Calls the local UnityMCP server directly. Request bodies go through a UTF-8 file:
# passing Korean text as a curl command-line argument gets mangled on Windows.
# If the cached session has expired (common after a domain reload), it reconnects once.
DIR=/d/PCUBE/UnderCooked/results
URL=http://127.0.0.1:8080/mcp
HDR=(-H "Content-Type: application/json; charset=utf-8" -H "Accept: application/json, text/event-stream")
PY=/c/Users/User/miniconda3/envs/mlagents/python.exe

init_session() {
  printf '%s' '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-03-26","capabilities":{},"clientInfo":{"name":"claude-direct","version":"1"}}}' > $DIR/.mcp_req.json
  SID=$(curl -s -D - -o /dev/null -X POST $URL "${HDR[@]}" --data-binary @$DIR/.mcp_req.json | grep -i mcp-session-id | awk '{print $2}' | tr -d '\r')
  if [ -z "$SID" ]; then echo "UnityMCP 서버에 연결할 수 없다 ($URL). Unity 에디터와 MCP 브리지를 확인할 것" >&2; exit 1; fi
  printf '%s' '{"jsonrpc":"2.0","method":"notifications/initialized"}' > $DIR/.mcp_req.json
  curl -s -o /dev/null -X POST $URL "${HDR[@]}" -H "mcp-session-id: $SID" --data-binary @$DIR/.mcp_req.json
  echo "$SID" > $DIR/.mcp_sid
}

call() {
  printf '{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"%s","arguments":%s}}' "$1" "${2:-{\}}" > $DIR/.mcp_req.json
  curl -s -X POST $URL "${HDR[@]}" -H "mcp-session-id: $SID" --data-binary @$DIR/.mcp_req.json
}

SID=$(cat $DIR/.mcp_sid 2>/dev/null)
[ -z "$SID" ] && init_session
RESP=$(call "$1" "$2")
if [ -z "$RESP" ] || echo "$RESP" | grep -q "Session not found"; then
  rm -f $DIR/.mcp_sid
  init_session
  RESP=$(call "$1" "$2")
fi

printf '%s\n' "$RESP" | sed -n 's/^data: //p' | $PY -c "
import sys,json
sys.stdin.reconfigure(encoding='utf-8')
sys.stdout.reconfigure(encoding='utf-8')
for line in sys.stdin:
    d=json.loads(line)
    if 'result' in d:
        for c in d['result'].get('content',[]): print(c.get('text',''))
    else: print(json.dumps(d,ensure_ascii=False))
"
