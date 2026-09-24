'use strict';
window.ClimateFX = (() => {
 const clamp=(x,a=0,b=1)=>Math.max(a,Math.min(b,x));
 const hash=n=>{const v=Math.sin(n*127.1+311.7)*43758.5453;return v-Math.floor(v);};
 let opts={level:0,phase:'severe',indoor:true,shader:false,particles:false,paused:false},gl,program,uniforms={},weather,ctx,previous=0,time=0,bursts=[],lastShaderError=null;
 function drawCardFrost(canvas,ice,style){const c=canvas.getContext('2d'),w=canvas.width,h=canvas.height;c.clearRect(0,0,w,h);if(ice<=0)return;const p=ice/200;
  const pixels=(x,y,size,color,alpha=1)=>{c.globalAlpha=alpha;c.fillStyle=color;c.fillRect(Math.round(x/2)*2,Math.round(y/2)*2,size,size);};
  // Card illustration only: name and value occupy the opaque lower safety strip.
  c.save();c.beginPath();c.rect(0,0,w,146);c.clip();
  if(style==='A'){
   for(let i=0;i<100;i++){if(hash(i+2)>p*.92)continue;const side=i%4,pos=hash(i+33),inset=hash(i+110)*(4+15*p);let x=0,y=0;
    if(side===0){x=pos*w;y=inset;}else if(side===1){x=w-inset-2;y=pos*140;}else if(side===2){x=pos*w;y=140-inset;}else{x=inset;y=pos*140;}
    pixels(x,y,2+2*Math.floor(hash(i+301)*2),'#b6dce9',.5+.4*p);
    if(i%3===0){pixels(x+2,y+4,2,'#78a9bd',.6);pixels(x+4,y+6,2,'#d9eff5',.75);}
   }
   for(let side=0;side<2;side++)for(let i=0;i<5;i++){const yy=14+i*26,len=(8+15*hash(i+19))*p;c.strokeStyle='#c2e5ee';c.globalAlpha=.5+p*.3;c.lineWidth=2;c.beginPath();c.moveTo(side?w:0,yy);c.lineTo(side?w-len:len,yy+len);c.lineTo(side?w-len-4:len+4,yy+len+9);c.stroke();}
   if(ice>=100){c.globalAlpha=.32;c.fillStyle='#9bbeca';c.fillRect(0,48,w,5);c.fillRect(0,91,w,3);}
  }else{
   const n=style==='B'?12:22;
   for(let i=0;i<n;i++){if(hash(i+220)>.25+p*.75)continue;const right=i%2===1,x=right?w:0,y=8+hash(i+78)*125,reach=(style==='C'?13:8)+p*(14+hash(i+61)*23),height=7+hash(i+9)*25;
    c.globalAlpha=(style==='C'?.24:.34)+p*.25;c.fillStyle=i%3===0?'#b9ddeb':'#6595b0';c.beginPath();c.moveTo(x,y);c.lineTo(right?x-reach*.55:reach*.55,y+height*.2);c.lineTo(right?x-reach:reach,y+height);c.lineTo(x,y+height+11);c.closePath();c.fill();c.globalAlpha=.5+.4*p;c.strokeStyle='#c4e9f5';c.lineWidth=2;c.beginPath();c.moveTo(x,y);c.lineTo(right?x-reach*.55:reach*.55,y+height*.2);c.lineTo(right?x-reach:reach,y+height);c.stroke();
   }
   if(style==='C'){for(let i=0;i<60;i++){const x=i%30*5,y=i<30?0:136;if(hash(i+5)>p)continue;pixels(x,y,4+Math.floor(p*5),'#c5e5ec',.4+p*.45);}if(ice>=100){c.globalAlpha=.11*p;c.fillStyle='#a9cddd';c.fillRect(5,6,w-10,133);}}
   if(ice>=100){c.lineWidth=3;c.strokeStyle='#cce7ef';c.globalAlpha=style==='C'?.5:.3;c.beginPath();c.moveTo(0,60);c.lineTo(40,68);c.lineTo(61,59);c.lineTo(83,72);c.lineTo(w,59);c.stroke();}
  }
  c.restore();c.globalAlpha=1;
 }
 const vertex=`attribute vec2 a_position; varying vec2 v_uv; void main(){v_uv=a_position*0.5+0.5;gl_Position=vec4(a_position,0.,1.);}`;
 const fragment=`precision highp float;
 varying vec2 v_uv; uniform float u_level; uniform float u_thaw; uniform vec2 u_resolution;
 vec2 h2(vec2 p){p=fract(p*vec2(.1031,.1030));p+=dot(p,p.yx+33.33);return fract((p.xx+p.yx)*p.xy);}
 float h(vec2 p){return h2(p).x;}
 float n(vec2 p){vec2 i=floor(p),f=fract(p);f=f*f*(3.0-2.0*f);return mix(mix(h(i),h(i+vec2(1,0)),f.x),mix(h(i+vec2(0,1)),h(i+vec2(1,1)),f.x),f.y);}
 vec2 cell(vec2 p){vec2 ip=floor(p),fp=fract(p);float a=8.,b=8.;for(int y=-1;y<=1;y++){for(int x=-1;x<=1;x++){vec2 g=vec2(float(x),float(y));vec2 d=g+h2(ip+g)-fp;float t=dot(d,d);if(t<a){b=a;a=t;}else if(t<b){b=t;}}}return vec2(a,b);}
 void main(){vec2 uv=floor(v_uv*u_resolution/2.0)*2.0/u_resolution;
 float edge=min(min(uv.x,1.-uv.x)*1.778,min(uv.y,1.-uv.y));float corner=pow(abs(uv.x-.5)*2.,3.)*pow(abs(uv.y-.5)*2.,3.);
 float noise=n(uv*vec2(24.,14.))*.65+n(uv*vec2(83.,47.))*.35;float width=(.006+u_level*.052)*(0.30+noise*.95+corner*.85);
 float frost=(1.-smoothstep(width*.22,width,edge))*u_level;
 vec2 crystal=cell(uv*vec2(52.,30.));float facet=1.-smoothstep(.015,.13,crystal.y-crystal.x);float glint=step(.79,noise)*.18;
 vec3 color=mix(vec3(.18,.33,.43),vec3(.73,.89,.93),facet*.72+noise*.18+glint);
 // Continuous screen-space coverage: never cut rectangular holes around UI labels.
 // The season/date glyphs are foreground UI, with transparent backgrounds.
 float alpha=frost*(.18+noise*.32+facet*.65)*mix(1.,.7,u_thaw);
 gl_FragColor=vec4(color,alpha);
 }`;
 function compile(type,source){const s=gl.createShader(type);gl.shaderSource(s,source);gl.compileShader(s);if(!gl.getShaderParameter(s,gl.COMPILE_STATUS))throw new Error(gl.getShaderInfoLog(s));return s;}
 function start(shaderCanvas,weatherCanvas){weather=weatherCanvas;ctx=weather.getContext('2d');try{gl=shaderCanvas.getContext('webgl',{alpha:true,premultipliedAlpha:false,antialias:false,preserveDrawingBuffer:true});if(!gl)throw new Error('WebGL unavailable');program=gl.createProgram();gl.attachShader(program,compile(gl.VERTEX_SHADER,vertex));gl.attachShader(program,compile(gl.FRAGMENT_SHADER,fragment));gl.linkProgram(program);if(!gl.getProgramParameter(program,gl.LINK_STATUS))throw new Error(gl.getProgramInfoLog(program));gl.useProgram(program);const b=gl.createBuffer();gl.bindBuffer(gl.ARRAY_BUFFER,b);gl.bufferData(gl.ARRAY_BUFFER,new Float32Array([-1,-1,1,-1,-1,1,-1,1,1,-1,1,1]),gl.STATIC_DRAW);const a=gl.getAttribLocation(program,'a_position');gl.enableVertexAttribArray(a);gl.vertexAttribPointer(a,2,gl.FLOAT,false,0,0);['level','thaw','resolution'].forEach(k=>uniforms[k]=gl.getUniformLocation(program,'u_'+k));gl.uniform2f(uniforms.resolution,shaderCanvas.width,shaderCanvas.height);gl.viewport(0,0,shaderCanvas.width,shaderCanvas.height);}catch(e){lastShaderError=e.message;console.error('Climate preview shader:',e);shaderCanvas.dataset.error=e.message;}
  requestAnimationFrame(frame);
 }
 function renderShader(){if(!gl||!program||lastShaderError)return;gl.useProgram(program);gl.uniform1f(uniforms.level,opts.shader?opts.level:0);gl.uniform1f(uniforms.thaw,opts.phase==='thaw'?1:0);gl.drawArrays(gl.TRIANGLES,0,6);}
 function drawWeather(){ctx.clearRect(0,0,weather.width,weather.height);if(opts.particles&&opts.level>0){
  // Canvas is half resolution. All ambient particles stay in the free desktop or outer rim.
  const count=Math.round((opts.indoor?10:38)*opts.level);
  for(let i=0;i<count;i++){const seed=i+50,thaw=opts.phase==='thaw',speed=thaw?18:opts.indoor?1.7:3.3;const x=428+hash(seed)*350+Math.sin(time*.25+i)*3,y=(55+hash(seed+71)*424+time*speed)%488;const size=hash(seed+30)>.75?2:1;ctx.globalAlpha=(.15+hash(seed+4)*.3)*opts.level;ctx.fillStyle=i%3===0?'#d3e8ed':'#83b5cb';ctx.fillRect(Math.round(x),Math.round(y),size,thaw?size*3:size);if(!thaw&&i%7===0){ctx.globalAlpha*=.5;ctx.fillRect(Math.round(x)-1,Math.round(y)+1,4,1);}}
 }
 for(const b of bursts){const age=time-b.time;if(age>1)continue;for(let i=0;i<14;i++){const a=hash(i)*Math.PI*2,dist=age*(12+hash(i+20)*30);ctx.globalAlpha=(1-age)*.85;ctx.fillStyle=i%2?'#c4e6ed':'#719eb7';ctx.fillRect(Math.round(b.x/2+Math.cos(a)*dist),Math.round(b.y/2+Math.sin(a)*dist+age*age*18),2,2);}}bursts=bursts.filter(b=>time-b.time<1);ctx.globalAlpha=1;
 }
 function frame(now){const delta=previous?Math.min(.05,(now-previous)/1000):0;previous=now;if(!opts.paused&&!document.hidden)time+=delta;drawWeather();requestAnimationFrame(frame);}
 function configure(options){opts={...opts,...options};renderShader();drawWeather();}
 function burst(x,y){bursts.push({x,y,time});drawWeather();}
 return {start,configure,drawCardFrost,burst,get shaderError(){return lastShaderError},get shaderReady(){return !!program&&!lastShaderError;}};
})();
