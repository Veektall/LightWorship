import json, re, sys
from pathlib import Path
from faster_whisper import WhisperModel

AUDIO=Path(sys.argv[1]); OUT=Path(sys.argv[2]); OUT.mkdir(parents=True, exist_ok=True)
model=WhisperModel('base.en', device='cpu', compute_type='int8', cpu_threads=4)
segments_iter, info=model.transcribe(str(AUDIO), beam_size=1, best_of=1, vad_filter=True, vad_parameters=dict(min_silence_duration_ms=500), condition_on_previous_text=True)
segments=[]
for s in segments_iter:
    text=re.sub(r'\s+',' ',s.text).strip()
    if text: segments.append({'start':round(float(s.start),3),'end':round(float(s.end),3),'text':text})
(OUT/'transcript.json').write_text(json.dumps({'language':info.language,'duration':info.duration,'segments':segments},ensure_ascii=False,indent=2),encoding='utf-8')

HOOK={'listen':2.0,'hear me':2.4,'watch this':2.5,'the truth':2.0,'the problem':2.0,'the reason':1.8,'you need to':1.7,'you have to':1.7,'never':1.4,'why':1.2,'what if':2.0,'imagine':2.0,'remember':1.5,'secret':2.2,'mistake':2.0,'right now':1.2,'god':0.5,'jesus':0.7,'faith':0.7,'prayer':0.7,'power':0.8,'grace':0.7,'enemy':0.8,'devil':0.8,'destiny':0.8,'miracle':1.0,'somebody':0.7,'tell you':0.8,'i want you':0.8,'you cannot':1.1,"you can't":1.1,'one thing':1.0,'most people':1.4}
def score(text,dur):
    low=text.lower(); words=re.findall(r"[a-zA-Z']+",low); n=len(words); sc=0.0
    for p,w in HOOK.items(): sc+=low.count(p)*w
    sc+=text.count('?')*1.5+text.count('!')
    sc+=min(n/95,2.5)
    for p in ['but ','however','because','so ','then ','until','finally','that is why','which means']: sc+=low.count(p)*0.45
    sc+=min((low.count(' you ')+low.count(' your '))*0.08,2.0)
    sc+=min(len(re.findall(r'\b\d+\b',low))*0.35,1.5)
    if n and len(set(words))/n<0.35: sc-=2
    if dur<42: sc-=2
    return sc
wins=[]; N=len(segments)
for i in range(N):
    st=segments[i]['start']; parts=[]
    if len(segments[i]['text'].split())<3: continue
    for j in range(i,N):
        dur=segments[j]['end']-st
        if dur>82: break
        parts.append(segments[j]['text'])
        if dur>=48:
            text=' '.join(parts); sc=score(text,dur)+(0.7 if text.rstrip().endswith(('.', '!', '?')) else 0)
            wins.append({'start':st,'end':segments[j]['end'],'duration':dur,'score':sc,'text':text})
wins.sort(key=lambda x:x['score'],reverse=True); selected=[]
for w in wins:
    if any(max(0,min(w['end'],x['end'])-max(w['start'],x['start']))>18 for x in selected): continue
    selected.append(w)
    if len(selected)>=60: break
def ts(sec):
    h=int(sec//3600); m=int((sec%3600)//60); s=sec%60; return f'{h:02d}:{m:02d}:{s:05.2f}'
with (OUT/'candidates.md').open('w',encoding='utf-8') as f:
    f.write(f'# Viral Clip Candidates\n\nLanguage: {info.language}; duration: {info.duration:.1f}s\n\n')
    for i,w in enumerate(selected,1):
        f.write(f"## {i}. {ts(w['start'])} -> {ts(w['end'])} ({w['duration']:.1f}s) score {w['score']:.2f}\n\n{w['text']}\n\n")
print((OUT/'candidates.md').read_text(encoding='utf-8'))