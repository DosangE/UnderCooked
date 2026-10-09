// 네이버 카페 게시용 프로젝트 소개 글(docx)을 만든다. 참고한 형식: 다른 팀의 최종 프로젝트 소개 글.
// 실행 (이 폴더에서): npm install docx@9 && node make_post.js "최종 프로젝트 - UnderCooked.docx"
// 그림: images/ (01~05). 02_협동_구조.png는 diagram.py로 만든다.
// 카페 에디터는 Word에서 붙여 넣을 때 그림을 빠뜨리기도 한다. 그때는 images/를 번호 순서대로 올린다.
const fs = require("fs");
const path = require("path");
const {
  Document, Packer, Paragraph, TextRun, ImageRun, Table, TableRow, TableCell, WidthType,
  ShadingType, AlignmentType, HeadingLevel, LevelFormat, BorderStyle, ExternalHyperlink,
} = require("docx");

const REPO = path.resolve(__dirname, "../..");
const HERE = __dirname;
const FONT = "맑은 고딕";
const CONTENT_W = 9026; // A4 width 11906 - margins 1440 * 2
const children = [];

// "**굵게**" 만 지원하는 간단한 인라인 마크업
function runs(text, opts = {}) {
  const out = [];
  text.split(/(\*\*[^*]+\*\*|`[^`]+`)/).forEach((part) => {
    if (!part) return;
    if (part.startsWith("**")) out.push(new TextRun({ text: part.slice(2, -2), bold: true, ...opts }));
    else if (part.startsWith("`")) out.push(new TextRun({ text: part.slice(1, -1), font: "Consolas", ...opts }));
    else out.push(new TextRun({ text: part, ...opts }));
  });
  return out;
}
const P = (t) => children.push(new Paragraph({ keepNext: t.startsWith("**-"), children: runs(t) }));
const GAP = () => children.push(new Paragraph({ children: [] }));
const H = (t) => { GAP(); children.push(new Paragraph({ heading: HeadingLevel.HEADING_1, children: [new TextRun(t)] })); };
const H2 = (t) => children.push(new Paragraph({ heading: HeadingLevel.HEADING_2, children: [new TextRun(t)] }));
const LI = (t, level = 0) => children.push(new Paragraph({ numbering: { reference: "bul", level }, children: runs(t) }));
const NUM = (t) => children.push(new Paragraph({ numbering: { reference: "num", level: 0 }, children: runs(t) }));
const CAP = (t) => children.push(new Paragraph({ alignment: AlignmentType.CENTER, children: [new TextRun({ text: t, size: 18, color: "6B6A66" })] }));
function CODE(text) {
  text.split("\n").forEach((line) => children.push(new Paragraph({
    shading: { type: ShadingType.CLEAR, color: "auto", fill: "F4F4F2" },
    spacing: { after: 0, line: 300 }, alignment: AlignmentType.LEFT,
    children: [new TextRun({ text: line || " ", font: "Consolas", size: 18 })],
  })));
  GAP();
}
function IMG(file, w, h, type = "png") {
  const width = 600, height = Math.round((h * 600) / w);
  children.push(new Paragraph({ alignment: AlignmentType.CENTER, children: [
    new ImageRun({ type, data: fs.readFileSync(file), transformation: { width, height } })] }));
}
const border = { style: BorderStyle.SINGLE, size: 4, color: "D9D8D3" };
const borders = { top: border, bottom: border, left: border, right: border };
function TABLE(rows, widths) {
  const total = widths.reduce((a, b) => a + b, 0);
  const cols = widths.map((w) => Math.round((w * CONTENT_W) / total));
  cols[cols.length - 1] += CONTENT_W - cols.reduce((a, b) => a + b, 0);
  children.push(new Table({
    width: { size: CONTENT_W, type: WidthType.DXA }, columnWidths: cols,
    rows: rows.map((r, i) => new TableRow({ children: r.map((c, j) => new TableCell({
      borders, width: { size: cols[j], type: WidthType.DXA },
      shading: i === 0 ? { type: ShadingType.CLEAR, color: "auto", fill: "F1F0EC" } : undefined,
      margins: { top: 60, bottom: 60, left: 100, right: 100 },
      children: [new Paragraph({ spacing: { after: 0 }, alignment: AlignmentType.LEFT,
        children: runs(c, { size: 19, bold: i === 0 ? true : undefined }) })],
    })) })),
  }));
  GAP();
}

// ---------------------------------------------------------------- 본문
children.push(new Paragraph({ children: [new TextRun({ text: "최종 프로젝트 - UnderCooked", bold: true, size: 36 })] }));
children.push(new Paragraph({ children: [new ExternalHyperlink({ link: "https://github.com/DosangE/UnderCooked",
  children: [new TextRun({ text: "https://github.com/DosangE/UnderCooked", style: "Hyperlink" })] })] }));
IMG(`${REPO}/assets/demo_blue.gif`, 640, 538, "gif");
CAP("두 셰프가 주문판을 보고 수프를 만들어 서빙하는 모습 (최종 모델, 23.4초에 3접시)");
GAP();
P("제가 선정한 최종 프로젝트 주제는 **두 에이전트가 협동해서 요리하는 주방**입니다.");
GAP();
P("Overcooked라는 게임을 아주 단순하게 줄인 주방을 Unity로 만들고, 두 셰프가 주문을 읽고 역할을 나눠 요리를 내는 법을 ML-Agents의 MA-POCA로 학습시켰습니다. 처음에는 재료 2종·요리 3종으로 만들었고, 이후 재료를 하나 더 넣어 재료 3종·요리 6종까지 넓혔습니다.");
GAP();
P("결론부터 말하면, 최종 모델은 45초 판에서 3접시 목표를 1105판 중 1099판(**99.5%**) 달성했습니다. 다만 초록이 들어가지 않은 요리는 주문판에 있어도 13~36%만 골라서, 요리 선택에는 편향이 남아 있습니다. 각 부분을 어떻게 만들었는지 정리해보겠습니다.");

H("Overcooked란?");
P("Overcooked는 2~4명이 한 주방에서 주문을 보고 재료 손질, 조리, 서빙을 나눠서 하는 협동 게임입니다. 주방이 좁거나 둘로 나뉘어 있어서 혼자서는 다 할 수 없고, 같이 하는 사람과 손발이 맞아야 점수가 납니다.");
GAP();
P("이 게임을 잘하는 팀은 두 가지를 합니다. 주문판을 보고 **무엇을 만들지** 정하고, 동료와 **누가 무엇을 나를지** 나눕니다. 두 가지 모두 정해진 정답이 없고 상황에 따라 바뀝니다. 그래서 여러 에이전트가 팀 보상 하나를 같이 받는 협동 강화학습 태스크로 적합하다고 생각했습니다.");

H("참고한 선행 프로젝트");
P("가장 잘 알려진 선행 연구는 Overcooked-AI (https://github.com/HumanCompatibleAI/overcooked_ai)입니다. 사람과 AI가 같이 플레이할 때 어떤 학습 방식이 협동을 잘하는지 연구하려고 만든 파이썬 격자 환경입니다. 레이아웃이 여러 개 있고, 그중에는 벽으로 나뉘어 있어 재료와 접시를 반드시 넘겨줘야 하는 레이아웃도 있습니다.");
GAP();
P("이 프로젝트는 그 아이디어를 Unity로 옮기면서, 주문을 읽고 요리를 고르는 문제를 더 키웠습니다. 차이를 정리하면 다음과 같습니다. (Overcooked-AI는 처음 공개된 논문 설정 기준입니다.)");
TABLE([
  ["", "Overcooked-AI", "내 프로젝트"],
  ["환경", "Python 격자 환경", "Unity + ML-Agents"],
  ["요리", "양파 수프 한 가지", "재료 2개 조합 6가지 (초록·빨강·파랑)"],
  ["주문", "어떤 수프든 내면 점수", "주문판 최대 3개, 각 25초. 주문과 맞는 요리만 점수"],
  ["협동", "레이아웃에 따라 다름", "모든 판에서 맵 구조로 강제"],
  ["학습", "PPO (셀프플레이 등)", "MA-POCA (팀 단위 보상, 셰프 2명이 한 팀)"],
], [1.2, 2.6, 3.6]);

H("제약");
P("실제 Overcooked를 그대로 만들기에는 고려할 것이 너무 많습니다. 학습할 문제를 다룰 수 있는 크기로 줄이려고 몇 가지 가정을 세웠습니다.");
GAP();
P("**- 격자 위의 작은 주방**");
P("9 × 9 격자에 셰프 두 명이 있습니다. 행동은 상하좌우 이동과 \"상호작용\" 버튼 하나뿐입니다. 집기, 놓기, 넣기, 담기, 내기는 모두 바라보는 칸에 상호작용을 하면 상황에 맞게 일어납니다.");
GAP();
P("**- 모든 요리는 재료 정확히 2개**");
P("재료 3종에서 2개를 고르는 조합(같은 재료 2개 포함) 6가지가 요리 전부입니다. 어떤 주문이 와도 작업량이 같아서, 학습이 풀어야 할 문제가 \"주문을 읽고 무엇을 만들지 정하기\"로 좁혀집니다. 냄비가 차면 반드시 어떤 요리가 됩니다.");
GAP();
P("**- 냄비 하나, 조리는 자동**");
P("냄비에 재료 2개가 들어가면 5초 뒤 저절로 완성됩니다. 불 조절이나 태우기는 없습니다.");
GAP();
P("**- 짧은 판**");
P("한 판은 45초입니다. 3접시를 내면 성공으로 바로 끝납니다. 주문은 제한 시간 25초가 지나면 사라지고 새 주문이 들어옵니다.");

H("기획: 협동을 보상이 아니라 맵으로 강제하기");
P("처음 고민은 \"어떻게 하면 두 셰프가 진짜로 협동하게 만들까\"였습니다. 협동했을 때 보너스를 주는 방법도 있지만, 그러면 협동하는 척만 하는 행동이 보상을 받기 쉽습니다.");
GAP();
P("그래서 **혼자서는 아예 요리를 낼 수 없는 맵**을 만들었습니다. 냄비는 북쪽 셰프 A 구역에만, 그릇함과 서빙구는 남쪽 셰프 B 구역에만 있습니다. 두 구역 사이에는 카운터 4칸이 있고 셰프는 이곳을 넘어갈 수 없습니다. 그래서 빈 그릇은 B에서 A로, 완성 요리는 A에서 B로 반드시 카운터를 건너야 합니다.");
GAP();
IMG(path.join(HERE, "images", "02_협동_구조.png"), 2400, 1080);
CAP("한 접시가 나가기까지의 흐름");
GAP();
P("재료함(초록·빨강·파랑)은 경계에 있어서 양쪽 다 쓸 수 있고, 손질대도 구역마다 하나씩 있습니다. 즉 **누가 어느 재료를 맡을지는 맵이 정해주지 않습니다.** 처음에는 손질대를 색으로 나눴는데(초록 손질대는 A, 빨강 손질대는 B), 그러면 재료 색이 담당자를 정해버려서 RedSoup에서 B가 A보다 4.3배 많이 움직였습니다. A는 냄비 앞에 서서 받기만 했습니다. 손질대를 구역으로 나누자 분담을 둘이 그때그때 정해야 하는 문제가 되었습니다.");
GAP();
P("하나 더 정한 것은 **주문은 보여주고, 조리 완료는 숨긴다**입니다.");
LI("주문판은 관측에 넣었습니다. 무엇을 만들지는 기억이 아니라 읽어야 하는 정보입니다. 주문판이 없으면 정책은 평균적으로 가장 이득인 요리 하나만 만들 것으로 예상했습니다(주문판을 뺀 비교 실험은 하지 않았습니다).");
LI("요리가 다 됐는지는 관측에서 뺐습니다. 재료를 언제 넣었는지 기억해야 제때 뜰 수 있게 하려는 의도로 기억(LSTM)을 켰습니다.");
LI("다만 이것만으로 기억이 강제되지는 않았습니다. 쉬운 조건에서 LSTM을 켠 런과 끈 런을 비교해 보니 끈 쪽이 더 빨리 배웠고, 켠 정책도 완료 시점을 기억하는 대신 냄비를 반복해서 떠보는 방식으로 풀었습니다. 최종 6종 조건에서 LSTM의 효과는 확인하지 않았습니다.");

H("ML-Agents 환경");
H2("에이전트와 학습 방식");
P("셰프 2명이 한 팀(SimpleMultiAgentGroup)이고, 같은 주방 16개를 동시에 돌려 셰프 32명이 함께 학습합니다. 알고리즘은 ML-Agents의 **MA-POCA**를 썼습니다.");
GAP();
P("서빙 점수는 팀 전체가 받기 때문에, 그냥 PPO를 각자 돌리면 \"방금 점수가 누구 덕분인지\"를 구분하기 어렵습니다. MA-POCA는 팀 전체의 가치를 한 번에 추정하면서, 각 에이전트의 기여도는 \"동료는 그대로 두고 내 행동만 달랐다면\"을 기준값으로 삼아 따로 계산합니다(기준값은 내 관측과 동료의 관측·행동을 보고 내 행동만 빼서 추정합니다). 두 셰프의 일이 서로 다른 이 문제에 맞는 방식이라고 판단했습니다.");
GAP();
P("목표를 채우면 그룹 에피소드를 성공으로 끝내고, 45초 시간 초과는 \"중단\"으로 처리합니다. 시간 초과를 끝으로 처리하면 \"시간이 지나면 가치가 0\"을 잘못 배우기 때문입니다.");

H2("관측");
P("셰프 한 명은 매 판단마다 145개의 숫자를 봅니다. 자기 위치만 정규화한 절대좌표이고, 동료·카운터·스테이션 위치는 모두 나를 기준으로 한 상대좌표입니다.");
TABLE([
  ["묶음", "개수", "내용"],
  ["나", "20", "위치 2 + 바라보는 방향 4 + 손에 든 것 14 (one-hot)"],
  ["동료", "16", "상대 위치 2 + 동료 손에 든 것 14"],
  ["냄비", "3", "재료별 개수. **조리가 끝났는지는 없음**"],
  ["카운터 4칸", "64", "칸마다 내용물 14 + 상대 위치 2"],
  ["스테이션", "16", "재료함 3, 냄비, 그릇함, 서빙구, 손질대 2의 상대 위치"],
  ["진행", "2", "남은 시간, 손질이 필요한지"],
  ["주문판", "24", "슬롯 3개 × (요리 6종 one-hot + 남은 시간 + 유효 여부)"],
], [1.4, 0.8, 5.2]);
P("주문 슬롯의 순서는 섞지 않았습니다. 같은 주문이 매번 같은 자리에 있어야 \"2번 슬롯이 급하다\"를 배울 수 있습니다.");

H2("행동");
LI("이산 행동 2개: 이동 5가지(정지/상/하/좌/우)와 상호작용 2가지(안 함/함)");
LI("행동 마스크: 벽으로 가는 이동, 지금 손 상태로는 할 게 없는 상호작용을 막았습니다.");
P("다만 **주문에 맞는 재료인지는 막지 않았습니다.** 그건 보상으로 배워야 할 실수이기 때문입니다.");

H2("보상");
P("기본 보상 구성은 다음과 같습니다(45초 평가도 이 값으로 했습니다). 팀 보상은 두 셰프가 같이 받고, 개인 보상은 해당 셰프만 받습니다. 단 카운터 전달 보상은 놓은 셰프와 집은 셰프 둘 다 받습니다.");
TABLE([
  ["항목", "값", "이유"],
  ["서빙 (주문과 일치)", "팀 +3.0", "유일한 실제 성과"],
  ["목표 달성 (3접시)", "팀 +2.0", "끝까지 채우게"],
  ["재료 투입", "팀 +0.3", "아직 대기 주문을 만들 수 있을 때만"],
  ["손질", "팀 +0.2", "서빙까지 가는 길이 길어서 중간 안내"],
  ["카운터 전달", "개인 +0.15 (양쪽)", "동료가 실제로 집어갔을 때, 물건마다 한 번"],
  ["주문 만료", "팀 −0.5", "주문을 버리지 않게"],
  ["주문에 없는 재료 투입", "개인 −0.3", "냄비가 어떤 주문도 못 만들게 된 순간"],
  ["주문에 없는 요리 서빙", "개인 −0.5", "아무거나 내는 것보다 안 하는 게 낫게"],
  ["매 스텝", "개인 −0.002", "빨리 끝내도록"],
], [2.4, 1.3, 3.7]);
P("최종 6종 모델의 마지막 두 런은 90초 판 / 목표 8접시 / 주문 30초 조건에서 만료 벌점을 −3으로 키우고 γ를 0.995로 올렸으며, 가장 급한 주문을 채운 서빙에 팀 보너스(+1.5, 이어서 +3.0)를 더해 학습했습니다. 이유는 아래 확장 절에 있습니다.");
GAP();
P("여기서 중요한 장치가 **회수(claw back)**입니다. 서빙까지는 재료 → 손질 → 투입 → 조리 → 빈 그릇 전달 → 뜨기 → 요리 전달 → 서빙으로 이어지는 긴 2인 체인이라, 중간 보상 없이는 초반에 학습 신호가 거의 없습니다. 그래서 중간 보상은 즉시 주되, 그 진행이 **서빙으로 이어지지 않으면 전부 되돌려 받습니다.** 냄비를 비우거나, 버리거나, 틀린 요리를 내거나, 판이 끝나면 그때까지 받은 중간 보상을 정산합니다.");

H("시행착오: 곡선은 올라가는데 서빙은 0");
P("학습을 한 번도 돌리기 전에 환경과 보상에서 19가지를 고쳤습니다. 가장 많은 시간이 든 것은 보상 어뷰징이었습니다. 카운터에 놓았다 집기만 반복해도 점수가 났고, 그걸 막으니 두 셰프가 물건을 주고받는 핑퐁이, 그걸 막으니 빈 그릇 왕복이 열렸습니다. 가장 심한 경로를 계산해보면 이렇습니다.");
TABLE([
  ["전략 (45초, 셰프 한 명)", "보상", "서빙"],
  ["손질 → 투입 → 비우기 반복", "+14 ~ +18", "0회"],
  ["정직하게 1접시", "+6.2", "1회"],
], [3.6, 1.6, 1.2]);
P("그대로 학습했다면 **보상 곡선은 예쁘게 오르는데 서빙은 0인** 정책이 나왔을 겁니다. 원인은 모두 \"되돌려야 하는 보상이 회수 체계 밖에 있었다\"였고, 위의 회수 장치가 그 답입니다. 고칠 때마다 Unity 안에서 실제 행동을 넣고 보상 변화만 읽는 회귀 검사 항목으로 남겼습니다(지금 17개).");
GAP();
P("커리큘럼에서도 한 번 크게 막혔습니다. ML-Agents가 커리큘럼 기준으로 비교하는 `Cumulative Reward`에는 **팀 보상이 들어 있지 않았습니다.** 접시 수 기준을 팀 보상 단위(3.0)로 잡아뒀는데 가장 쉬운 단계(1접시)에서 개인 보상은 최대 약 +0.3이라 절대 넘어갈 수 없는 관문이었습니다.");
GAP();
P("첫 런은 1.44M 스텝 동안 **서빙을 한 번도 못 했습니다.** 첫 서빙에 약 2M 스텝이 걸리는데, 커리큘럼이 그 전에 레시피와 손질을 켜버렸습니다. 서빙 보상을 한 번도 못 본 채 난이도만 올라간 겁니다. 그래서 가장 쉬운 난이도로 먼저 서빙을 배우게 한 뒤 커리큘럼을 돌리는 순서로 바꿨습니다.");

H("학습 결과 (재료 2종)");
TABLE([
  ["런", "설정", "스텝 / 시간", "목표 달성 (학습 중)"],
  ["v1", "기본 커리큘럼, 처음부터", "1.44M / 19분", "서빙 0회, 중단"],
  ["lesson0", "가장 쉬운 난이도 고정", "3M / 39분", "1.96M에 첫 서빙"],
  ["v2", "커리큘럼, lesson0에서 이어서", "8M / 1시간 39분", "54%"],
  ["final", "최종 난이도 고정", "3M / 39분", "74%"],
  ["final2", "같은 설정 이어서", "3M / 40분", "90.5%"],
  ["final3", "같은 설정 이어서", "3M / 40분", "**97.8%**"],
], [1.1, 2.9, 1.9, 1.5]);
IMG(`${REPO}/assets/tb_goal_reached.png`, 1500, 630);
CAP("재료 2종 학습 곡선 (런 5개를 이어 붙임)");
GAP();
P("곡선 가운데의 급락(4.2M~4.6M)은 커리큘럼이 레시피 2종과 손질을 연달아 켠 구간입니다. 생재료를 냄비에 넣던 정책이 냄비를 한 번도 못 채우게 됐다가, 스스로 손질 경로를 찾아 냄비 채우기는 약 1M 스텝, 서빙은 약 2M 스텝 뒤에 돌아왔습니다.");
GAP();
P("결과를 잘못 읽은 적도 있습니다. final3에서 잘못된 재료 벌점을 −0.1에서 −0.3으로 올렸고 90.5% → 97.8%를 벌점 덕분이라고 적었습니다. 그런데 시드 3개씩 대조해보니 −0.1로도 96.7%였습니다. 향상은 대부분 추가 학습 덕분이었습니다. 런 하나로 원인을 단정하면 안 된다는 걸 배웠습니다.");

H("확장: 재료 3종 · 요리 6종");
P("재료 2종 모델이 잘 되자 파랑 재료를 넣어 요리를 6종으로 늘렸습니다. 작업량은 그대로이고 \"어느 재료함에서 가져오는가\"만 늘어나서 쉬울 줄 알았는데, 두 가지 문제가 나왔습니다.");
H2("1. 새 재료를 안 배운다: 지름길");
P("주문 슬롯이 3개라서 이미 아는 GreenSoup·MixSoup만 만들어도 성공률이 45~72%가 나왔습니다. 성공률 기준 커리큘럼이 이 지름길에 속아서, 파랑은 거의 쓰지 않은 채(진단 600k 동안 파랑이 들어간 냄비 1개) 단계가 올라갔습니다.");
GAP();
P("해결은 단계마다 **이미 익힌 요리를 주문에서 빼는 것**이었습니다. 7단계는 BlueSoup만, 8단계는 지름길 2종을 뺀 4종만 주문합니다. 그 뒤 6종을 섞어 이어 학습하자 26% → 93%가 됐습니다.");
H2("2. 초록 없는 요리를 버린다");
P("6종을 다 만들 줄 알면서도 RedSoup·RedBlueSoup 주문을 피했습니다. 90초 조건에서 보면 이 두 요리 주문의 절반가량이 만료됐습니다. 목표 접시를 채우면 판이 바로 끝나서 모든 주문을 처리할 필요가 없고 어느 요리든 서빙 보상이 같으니, 정책은 \"일단 초록으로 시작하기\"를 굳혔습니다. 주문 3칸이 균등하게 뽑힌다면 초록 요리가 하나라도 있을 확률은 87.5%입니다(실제 플레이 기록에서는 82%).");
GAP();
P("효과가 없었던 시도가 많았습니다.");
LI("RedSoup만 따로 학습시킨 뒤 다시 섞기 → 다시 잊었습니다.");
LI("판 길이를 90초로 늘려 만료가 판 안에서 일어나게 하기 → 만료는 늘었는데 습관은 그대로였습니다.");
LI("만료 벌점을 −3으로 키우고 γ를 0.995로 올리기 → 그래도 그대로였습니다.");
P("만료 벌점은 무엇을 만들지 고른 뒤 10~30초나 늦게 옵니다. 그 사이에 γ 0.99에서는 0.05~0.37배, 0.995로 올려도 0.22~0.61배로 할인됩니다. 이렇게 늦고 약해진 신호라서 이미 굳은 습관을 바꾸지 못했다고 봅니다(따로 검증한 것은 아닙니다).");
GAP();
P("처음으로 움직인 것은 **서빙하는 순간에 바로 주는 보너스**였습니다. 서빙한 주문이 그 순간 주문판에서 가장 급한 주문이면 팀 보너스를 줍니다.");
CODE(`// KitchenGroup.FixedUpdate - 가장 급한 주문을 채운 서빙 보너스
int urgent = env.TakeUrgentServeCount();
if (urgent > 0)
{
    m_UrgentServes += urgent;
    if (env.UrgentServeBonus > 0f) AddTeamReward(env.UrgentServeBonus * urgent);
}`);
P("남은 시간이 아니라 순위만 봅니다. 주문판이 그대로인 동안에는 모든 주문의 시간이 같이 줄어서 기다려도 순위가 바뀌지 않지만, 더 급한 주문이 만료돼 새 주문으로 바뀌면 순위가 바뀔 수는 있습니다. 보너스 1.5를 넣은 뒤 RedSoup 선택이 6.5% → 10.8%로 처음 올랐고, 서빙 보상과 같은 3.0으로 키우고 학습률을 다시 시작하자 16.6%가 됐습니다. 런 하나씩이라 보너스와 추가 학습의 효과를 나누지는 못했습니다.");
GAP();
IMG(`${REPO}/assets/blue_pick_rate.png`, 1500, 660);
CAP("요리별 선택 비율: 주문판에 그 요리가 있을 때 실제로 만든 비율");
GAP();
IMG(`${REPO}/assets/tb_blue_goal_reached.png`, 1650, 690);
CAP("재료 3종 학습 곡선 (런 11개를 이어 붙임, 구간마다 조건이 다름)");
GAP();
P("학습 시간은 재료 2종 단계가 보완 실험까지 런 30개 약 25시간, 재료 3종 확장이 런 11개 약 22시간으로 합쳐서 약 47시간입니다(RTX 2080 SUPER, 1M 스텝당 약 13분).");

H("최종 결과");
P("학습 없이 추론 모드(mlagents-learn --inference)로 600k 스텝 돌리면서 판마다 기록해서 셌습니다. 학습된 정책이 Python 쪽에서 Unity 환경을 움직이는 방식이고, 내보낸 .onnx와 가중치는 같습니다.");
TABLE([
  ["모델", "판 수", "목표 달성", "잘못 채운 판", "성공한 판 길이 (중앙값)"],
  ["재료 2종 · 요리 3종 (final3)", "1100", "97.5%", "16.7%", "25.2초"],
  ["**재료 3종 · 요리 6종 (최종)**", "**1105**", "**99.5%**", "11.0%", "23.9초"],
], [2.8, 1.0, 1.2, 1.3, 1.5]);
P("6종 모델은 학습량(약 100M 스텝)과 보상 설정이 2종 모델(20M 스텝)과 달라서 요리 수만의 효과로 비교할 수는 없지만, 요리가 두 배인 조건에서도 99.5%를 냈습니다. 레시피를 틀린 판에서도 버리고 다시 만들어 96.7%를 성공했습니다. 실패한 6판은 모두 45초를 다 쓰고 주문 3개가 만료된 판이었습니다.");
GAP();
P("역할 분담도 맵이 정해주지 않은 부분까지 둘이 나눴습니다. 최종 모델의 냄비 기록(카운터 상태로 공급자를 추정)을 보면 첫 재료는 B가 손질해서 넘겨준 경우가 84%, A가 직접 가져온 경우가 16%였습니다. 두 번째 재료도 77%를 B가 댔습니다. 냄비 쪽 일(투입·뜨기)은 맵 때문에 A만 할 수 있으니, 재료 준비는 B가 더 맡는 쪽으로 분담이 기운 것입니다.");

H("한계");
P("가장 큰 한계는 **요리 선택의 편향**이 아직 남아 있다는 점입니다. 최종 모델도 초록이 들어간 요리는 주문판에 있을 때 47~90% 고르지만, 초록이 없는 요리는 13~36%만 고릅니다. 지금은 요리를 one-hot으로 주기 때문에 \"RedSoup = 빨강 2개\"를 정책이 따로 외워야 합니다.");
GAP();
P("또 최종 모델은 런 여러 개를 이어 붙여 만든 것이고 시드 하나로만 확인했습니다. 무작위 초기화부터 한 번에 학습해도 같은 결과가 나오는지는 아직 확인하지 못했습니다. 최종 성공률도 추론 모드로 잰 값이고, 내보낸 .onnx를 Unity 안에서 직접 돌려 판 수를 센 평가는 아직 하지 않았습니다.");
GAP();
P("다음으로는 주문과 냄비 내용물을 같은 \"재료 구성\" 형식으로 주는 관측 변경, 서빙 보너스를 처음부터 켠 채 한 번에 학습하기, 처리 속도에 맞춘 주문 간격 조정을 해볼 계획입니다.");

H("마치며");
P("가장 크게 배운 것은 **보상이나 성공률이 오른다고 원하는 행동을 배운 것은 아니라는 점**입니다. 서빙 없이 보상만 쌓이는 경로, 팀 보상이 빠진 커리큘럼 지표, 아는 요리만으로 성공률을 채우는 지름길은 모두 그 숫자만 봐서는 드러나지 않았고, 서빙 수와 냄비 기록을 따로 세어 보고 나서야 보였습니다.");
GAP();
P("런 하나로 원인을 단정하면 안 된다는 것도 배웠습니다. 벌점 덕분이라고 적었던 향상이 시드를 늘려 보니 추가 학습 덕분이었던 것처럼, 보너스가 편향을 줄였다는 지금의 해석도 대조 실험으로 확인해야 할 부분으로 남아 있습니다.");

// ---------------------------------------------------------------- 문서
const doc = new Document({
  styles: {
    default: { document: { run: { font: FONT, size: 21 } } },
    paragraphStyles: [
      { id: "Heading1", name: "Heading 1", basedOn: "Normal", next: "Normal", quickFormat: true,
        run: { size: 30, bold: true, font: FONT, color: "111111" }, paragraph: { spacing: { before: 240, after: 160 }, outlineLevel: 0, keepNext: true, keepLines: true } },
      { id: "Heading2", name: "Heading 2", basedOn: "Normal", next: "Normal", quickFormat: true,
        run: { size: 25, bold: true, font: FONT, color: "222222" }, paragraph: { spacing: { before: 200, after: 120 }, outlineLevel: 1, keepNext: true, keepLines: true } },
    ],
  },
  numbering: { config: [
    { reference: "bul", levels: [
      { level: 0, format: LevelFormat.BULLET, text: "•", alignment: AlignmentType.LEFT, style: { paragraph: { indent: { left: 600, hanging: 300 } } } },
      { level: 1, format: LevelFormat.BULLET, text: "–", alignment: AlignmentType.LEFT, style: { paragraph: { indent: { left: 1000, hanging: 300 } } } } ] },
    { reference: "num", levels: [
      { level: 0, format: LevelFormat.DECIMAL, text: "%1.", alignment: AlignmentType.LEFT, style: { paragraph: { indent: { left: 600, hanging: 300 } } } } ] },
  ] },
  sections: [{ properties: { page: { size: { width: 11906, height: 16838 },
    margin: { top: 1701, right: 1440, bottom: 1440, left: 1440 } } }, children }],
});
Packer.toBuffer(doc).then((buf) => { fs.writeFileSync(process.argv[2], buf); console.log("written", process.argv[2]); });
