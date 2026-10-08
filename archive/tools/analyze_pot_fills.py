"""Summarize a pot-fill log from inference/pot_fill_log.cs.

Usage: python archive/tools/analyze_pot_fills.py <fills.csv>
Recipes: 0 G2, 1 GR, 2 R2, 3 B2, 4 GB, 5 RB. Ingredients: 0 G, 1 R, 2 B.
"""
import csv, sys, collections as C
REQ = {0:(2,0,0),1:(1,1,0),2:(0,2,0),3:(0,0,2),4:(1,0,1),5:(0,1,1)}
NAME = {0:'G2',1:'GR',2:'R2',3:'B2',4:'GB',5:'RB'}
ING = {0:'G',1:'R',2:'B'}
def orders(s): return [int(x.split(':')[0]) for x in s.split('|') if x]
rows = list(csv.DictReader(open(sys.argv[1])))
com = [r for r in rows if r['event']=='commit' and r['first'] not in ('?','') and r['second'] not in ('?','')]
dumps = [r for r in rows if r['event']=='dump']
print('commits with known order', len(com), '/ all commits', sum(r['event']=='commit' for r in rows), '| dumps', len(dumps))
wrong = [r for r in com if r['wrong']=='1']
print(f'wrong commits {len(wrong)} ({len(wrong)/len(com):.0%})')

# 1) first ingredient
fi = C.Counter(ING[int(r['first'])] for r in com)
print('\n[1] first ingredient:', dict(fi))
demand = C.Counter()
for r in com:
    for o in orders(r['ordersAtFirst']):
        for i,n in enumerate(REQ[o]): demand[ING[i]] += n
tot = sum(demand.values()); print('    ingredient demand in orders at that moment:', {k: f'{v/tot:.0%}' for k,v in demand.items()})
def fits(ing, ords): return any(REQ[o][ing] > 0 for o in ords)
fw = [r for r in com if not fits(int(r['first']), orders(r['ordersAtFirst']))]
print(f'    first ingredient fits NO order: {len(fw)} ({len(fw)/len(com):.0%}) ->', dict(C.Counter(ING[int(r["first"])] for r in fw)))

# 2) classify wrong commits
cls = C.Counter(); detail = C.Counter()
for r in wrong:
    f, s = int(r['first']), int(r['second'])
    o1, o2 = orders(r['ordersAtFirst']), orders(r['ordersAtSecond'])
    if not fits(f, o1): cls['a first ingredient fit no order'] += 1; continue
    intended = [o for o in o1 if REQ[o][f] > 0]
    still = [o for o in intended if o in o2]
    if not still: cls['c the order(s) it fit expired before 2nd'] += 1; continue
    cls['b first ok, wrong 2nd ingredient'] += 1
    need = sorted({next(i for i in range(3) if REQ[o][i] - (1 if i==f else 0) > 0) for o in still})
    detail[(ING[f], ING[s], '/'.join(ING[i] for i in need))] += 1
print('\n[2] why wrong:'); [print(f'    {k}: {v} ({v/len(wrong):.0%})') for k,v in cls.most_common()]
print('    (b) first, put 2nd, needed 2nd:'); [print('      ', k, v) for k,v in detail.most_common(8)]

# 3) source of the wrong 2nd ingredient
print('\n[3] source of 2nd ingredient  wrong:', dict(C.Counter(r['secondSrc'] for r in wrong)), ' right:', dict(C.Counter(r['secondSrc'] for r in com if r['wrong']=='0')))
print('    source of 1st ingredient  all:', dict(C.Counter(r['firstSrc'] for r in com)))

# 4) per recipe: how often ordered vs made correctly
# made-right counts only pots whose dish was already on the board at the FIRST ingredient, so it
# is a subset of 'ordered' (pick rate = made-right / ordered). Pots that matched only an order
# that appeared between the two ingredients are counted separately as 'later'.
# (Before 2026-10-08 made-right also counted those, which put eval_blue_final's G2 at 108%.)
ordered = C.Counter(); made = C.Counter(); later = C.Counter()
for r in com:
    o1 = set(orders(r['ordersAtFirst']))
    for o in o1: ordered[o] += 1
    if r['wrong']=='0':
        c = int(r['cooked'])
        if c in o1: made[c] += 1
        else: later[c] += 1
print('\n[4] recipe: present in orders at first-fill / made correctly (on that board) / matched a later order / wrong-made')
wm = C.Counter(int(r['cooked']) for r in wrong)
for k in range(6): print(f'    {NAME[k]}: ordered {ordered[k]:4d}  made-right {made[k]:4d}  later {later[k]:3d}  made-wrong {wm[k]:4d}')
print('\n[5] dumps first ingredient:', dict(C.Counter(ING[int(r['first'])] for r in dumps if r['first'] not in ('','?','-'))))
