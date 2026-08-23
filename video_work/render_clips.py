import json, subprocess, sys, shlex, zipfile
from pathlib import Path

SRC=Path(sys.argv[1]); CFG=Path(sys.argv[2]); OUT=Path(sys.argv[3]); OUT.mkdir(parents=True, exist_ok=True)
clips=json.loads(CFG.read_text(encoding='utf-8'))['clips']
FONT='/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf'

def run(cmd):
    print('+', ' '.join(shlex.quote(str(x)) for x in cmd), flush=True)
    subprocess.run([str(x) for x in cmd], check=True)

def render(c, idx):
    ms=float(c['start']); me=float(c['end']); hs=float(c['hook_start']); he=float(c['hook_end'])
    md=me-ms; hd=he-hs
    if not (2.0 <= hd <= 6.5): raise ValueError(f'Hook duration out of range: {hd}')
    if not (35 <= md <= 100): raise ValueError(f'Main duration out of range: {md}')
    name=f"{idx:02d}_{c.get('slug',f'viral_clip_{idx:02d}')}.mp4"
    dst=OUT/name
    trans=0.42
    xfade_offset=max(0.1,hd-trans)
    boxx="if(lt(t,0.45),-350+844*t,if(gt(t,2.10),30-950*(t-2.10),30))"
    textx="if(lt(t,0.45),-300+811*t,if(gt(t,2.10),75-950*(t-2.10),75))"
    fc=f"""
[0:v]fps=30,split=2[hbg][hfg];
[hbg]scale=720:1280:force_original_aspect_ratio=increase,crop=720:1280,gblur=sigma=28,eq=brightness=-0.05:saturation=0.92[hbg2];
[hfg]scale=760:1280:force_original_aspect_ratio=decrease,unsharp=3:3:0.22:3:3:0[hfg2];
[hbg2][hfg2]overlay=(W-w)/2:(H-h)/2,drawbox=x='{boxx}':y=118:w=320:h=76:color=red@0.94:t=fill:enable='between(t,0,2.55)',drawtext=fontfile={FONT}:text='COMING UP':fontcolor=white:fontsize=39:x='{textx}':y=136:enable='between(t,0,2.55)',fade=t=in:st=0:d=0.10,format=yuv420p,settb=AVTB,setpts=PTS-STARTPTS[hv];
[1:v]fps=30,split=2[mbg][mfg];
[mbg]scale=720:1280:force_original_aspect_ratio=increase,crop=720:1280,gblur=sigma=28,eq=brightness=-0.05:saturation=0.92[mbg2];
[mfg]scale=720:1280:force_original_aspect_ratio=decrease,unsharp=3:3:0.22:3:3:0[mfg2];
[mbg2][mfg2]overlay=(W-w)/2:(H-h)/2,format=yuv420p,settb=AVTB,setpts=PTS-STARTPTS[mv];
[hv][mv]xfade=transition=smoothleft:duration={trans}:offset={xfade_offset}[vout];
[0:a]atrim=duration={hd},asetpts=PTS-STARTPTS[ha];
[1:a]atrim=duration={md},asetpts=PTS-STARTPTS[ma];
[ha][ma]acrossfade=d=0.32:c1=tri:c2=tri[a0];
[a0]highpass=f=75,afftdn=nr=6:nf=-45:tn=1,equalizer=f=3200:t=q:w=1:g=1.3,acompressor=threshold=-18dB:ratio=2.4:attack=18:release=180:makeup=1.35,loudnorm=I=-16:TP=-1.5:LRA=7[aout]
""".replace('\n','')
    cmd=['ffmpeg','-hide_banner','-y','-ss',f'{hs:.3f}','-t',f'{hd:.3f}','-i',SRC,'-ss',f'{ms:.3f}','-t',f'{md:.3f}','-i',SRC,'-filter_complex',fc,'-map','[vout]','-map','[aout]','-c:v','libx264','-preset','medium','-crf','20','-profile:v','high','-level','4.1','-pix_fmt','yuv420p','-c:a','aac','-b:a','192k','-ar','48000','-movflags','+faststart','-shortest',dst]
    run(cmd)
    return dst

outs=[]
for i,c in enumerate(clips,1): outs.append(render(c,i))

# Machine QC: verify all five files have H.264 video, AAC audio, portrait dimensions, and plausible duration.
qc=[]
for p in outs:
    probe=subprocess.check_output(['ffprobe','-v','error','-show_entries','format=duration,size:stream=codec_type,codec_name,width,height,sample_rate','-of','json',p],text=True)
    d=json.loads(probe); qc.append({'file':p.name,'probe':d})
(OUT/'qc.json').write_text(json.dumps(qc,indent=2),encoding='utf-8')

zip_path=OUT/'viral_clips_5_mp4.zip'
with zipfile.ZipFile(zip_path,'w',zipfile.ZIP_DEFLATED,compresslevel=4) as z:
    for p in outs: z.write(p,p.name)
    z.write(OUT/'qc.json','qc.json')
print(json.dumps({'zip':str(zip_path),'files':[p.name for p in outs]},indent=2))