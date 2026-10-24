from PIL import Image, ImageDraw, ImageFont
from pathlib import Path

W,H=960,600
out=Path('docs/assets/parcelcrop-demo.gif')
UPS_PAGE=Path('docs/assets/parcelcrop-ups-a4.png')
FEDEX_PAGE=Path('docs/assets/parcelcrop-fedex-a4.png')
# The carrier labels in the reference PDFs occupy only the upper-left portion
# of an A4 page. Keep this crop in normalized page coordinates so the demo
# shows the same detection target at every display size.
LABEL_BOX=(0.05,0.035,0.48,0.49)
C={'bg':'#eef2f1','ink':'#2c3639','muted':'#71807f','accent':'#1f7778','line':'#e3e9e7','panel':'#f7f9f8','pale':'#e5f0ee','danger':'#ad483a','white':'#ffffff'}
font_root=Path('C:/Windows/Fonts')
def f(name,size):
 p=font_root/name
 return ImageFont.truetype(str(p),size) if p.exists() else ImageFont.load_default()
F_HEAD=f('consola.ttf',22); F_SPEC=f('consola.ttf',12); F_BODY=f('consola.ttf',13); F_SMALL=f('consola.ttf',11); F_TINY=f('consola.ttf',9); F_BOLD=f('consolab.ttf',13)

def center(d,box,text,ft,fill):
 b=d.textbbox((0,0),text,font=ft); d.text((box[0]+(box[2]-box[0]-b[2]+b[0])/2,box[1]+(box[3]-box[1]-b[3]+b[1])/2),text,font=ft,fill=fill)
def button(d,box,text,primary=False):
 d.rounded_rectangle(box,6,fill=C['accent'] if primary else C['white'],outline=None if primary else C['line'],width=1)
 center(d,box,text,F_SMALL,C['white'] if primary else C['muted'])
def page_source(path):
    """Load a generated A4 reference page, with a white fallback for reruns."""
    if path.exists():
        return Image.open(path).convert('RGB')
    return Image.new('RGB',(1055,1491),'white')
def draw_source_page(im, box, page):
    """Show the full A4 page and outline the detected upper-left label."""
    x0,y0,x1,y1=box; w,h=x1-x0,y1-y0
    d=ImageDraw.Draw(im)
    d.rectangle(box,fill='#eef2f1',outline=C['line'],width=1)
    scaled=page.copy(); scaled.thumbnail((w-12,h-12),Image.Resampling.LANCZOS)
    px=x0+(w-scaled.width)//2; py=y0+(h-scaled.height)//2
    im.paste(scaled,(px,py))
    lx0=px+round(scaled.width*LABEL_BOX[0]); ly0=py+round(scaled.height*LABEL_BOX[1])
    lx1=px+round(scaled.width*LABEL_BOX[2]); ly1=py+round(scaled.height*LABEL_BOX[3])
    d.rectangle((lx0,ly0,lx1,ly1),outline=C['accent'],width=2)
def draw_output_label(im, box, page):
    """Show the detected carrier label cropped from the A4 source page."""
    x0,y0,x1,y1=box; w,h=x1-x0,y1-y0
    d=ImageDraw.Draw(im)
    d.rectangle(box,fill='#eef2f1',outline=C['line'],width=1)
    pw,ph=page.size
    crop=page.crop((round(pw*LABEL_BOX[0]),round(ph*LABEL_BOX[1]),round(pw*LABEL_BOX[2]),round(ph*LABEL_BOX[3])))
    crop.thumbnail((w-16,h-16),Image.Resampling.LANCZOS)
    im.paste(crop,(x0+(w-crop.width)//2,y0+(h-crop.height)//2))
def app_base(step,title,subtitle,with_queue=True):
 im=Image.new('RGB',(W,H),C['bg']); d=ImageDraw.Draw(im)
 d.rounded_rectangle((26,22,W-26,H-22),12,fill=C['white'],outline=C['line'],width=1)
 d.text((54,47),'Shipping labels',font=F_HEAD,fill=C['ink']); d.text((54,80),'4 x 6 in  /  1600 x 2400 px  /  400 dpi',font=F_SPEC,fill=C['muted'])
 d.text((54,123),title,font=F_SMALL,fill=C['accent']); d.text((54,143),subtitle,font=F_TINY,fill=C['muted'])
 # progress dots communicate simple flow without changing app controls
 for i in range(3):
  x=W-151+i*24; d.ellipse((x,55,x+10,65),fill=C['accent'] if i<step else '#dbe6e3')
 if with_queue:
  d.line((54,536,W-54,536),fill=C['line'],width=1)
  d.text((54,550),'JPGs save beside the PDFs. Original PDFs are kept by default.',font=F_TINY,fill=C['muted'])
 else:
  d.line((54,536,W-54,536),fill=C['line'],width=1); d.text((54,550),'Local processing • simple controls • no extra setup',font=F_TINY,fill=C['muted'])
 return im,d

def empty():
 im,d=app_base(1,'ADD FILES','Drop PDFs here, or browse a folder.',False)
 d.rounded_rectangle((54,180,906,512),8,fill=C['panel'],outline=C['line'],width=1)
 center(d,(54,264,906,340),'Add PDF files',F_HEAD,C['muted']); center(d,(54,340,906,370),'Drop PDFs here, or browse a folder.',F_SMALL,C['muted']); button(d,(386,392,574,430),'Browse files',True)
 return im

def preview():
 im,d=app_base(2,'CHECK PREVIEW','Automatic crop is ready. Adjust only when needed.')
 ups=page_source(UPS_PAGE)
 d.rectangle((54,180,678,512),fill=C['panel'],outline=C['line'],width=1)
 d.text((74,194),'CROP',font=F_SMALL,fill='#4f605f'); d.text((388,194),'OUTPUT',font=F_SMALL,fill='#4f605f')
 d.line((367,214,367,493),fill=C['line'],width=1)
 draw_source_page(im,(76,220,352,493),ups); draw_output_label(im,(382,220,656,493),ups)
 button(d,(494,188,550,212),'Detect'); button(d,(558,188,620,212),'Rotate')
 d.text((706,190),'FILES (2)',font=F_SMALL,fill=C['ink']); button(d,(814,186,866,212),'Clear')
 for y,name in [(234,'ups_ground.pdf'),(302,'fedex_ground.pdf')]:
  d.rounded_rectangle((700,y,880,y+52),6,fill=C['pale'] if y==234 else C['white'],outline=C['line'],width=1); d.text((714,y+10),name,font=F_SMALL,fill=C['ink']); d.text((714,y+31),'Ready',font=F_TINY,fill=C['accent'])
 button(d,(700,458,880,502),'Process all',True); button(d,(700,420,786,450),'Add files')
 return im

def exporting():
 im,d=app_base(3,'EXPORT JPG','The queue finishes together; source PDFs stay in place.')
 fedex=page_source(FEDEX_PAGE)
 d.rectangle((54,180,678,512),fill=C['panel'],outline=C['line'],width=1); d.text((74,194),'CROP',font=F_SMALL,fill='#4f605f'); d.text((388,194),'OUTPUT',font=F_SMALL,fill='#4f605f'); d.line((367,214,367,493),fill=C['line'],width=1); draw_source_page(im,(76,220,352,493),fedex); draw_output_label(im,(382,220,656,493),fedex)
 d.text((706,190),'FILES (2)',font=F_SMALL,fill=C['ink'])
 for y,name in [(234,'ups_ground.jpg'),(302,'fedex_ground.jpg')]:
  d.rounded_rectangle((700,y,880,y+52),6,fill=C['white'],outline=C['line'],width=1); d.text((714,y+10),name,font=F_SMALL,fill=C['ink']); d.text((714,y+31),'Saved / PDF kept',font=F_TINY,fill=C['accent'])
 d.rounded_rectangle((700,420,880,458),6,fill=C['pale']); center(d,(700,420,880,458),'2 completed',F_SMALL,C['accent']); button(d,(700,468,786,500),'Open folder')
 return im

frames=[empty(),preview(),exporting()]
frames[0].save(out,save_all=True,append_images=frames[1:],duration=[1500,1900,2200],loop=0,optimize=True,disposal=2)
print(out, out.stat().st_size)
