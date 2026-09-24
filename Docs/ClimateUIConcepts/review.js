async (page) => {
  const checks = [], errors = [];
  page.on('pageerror', e => errors.push(e.message));
  const check = (ok, text) => { if (!ok) throw new Error(text); checks.push(text); };
  await page.setViewportSize({ width: 1992, height: 1440 });
  await page.goto('http://127.0.0.1:8879/');
  await page.evaluate(() => document.fonts.ready);
  await page.waitForFunction(() => document.documentElement.dataset.ready === 'true');
  check(await page.evaluate(() => ClimateFX.shaderReady), 'WebGL shader compiled and linked');
  check(await page.evaluate(() => [...document.images].every(i => i.complete && i.naturalWidth > 0)), 'All original game assets loaded');
  const designs = ['A 紧凑维护单 熟悉、直接、信息集中', 'B 模块装配板 槽位清楚，强调拖入安装', 'C 设备检修台 逐项选择，操作区更从容'];
  const scenes = [['cabin','舱室'],['water','水域'],['passage','双向通道']];
  for (let i=0; i<3; i++) {
    const letter = 'ABC'[i];
    await page.getByRole('button', {name: designs[i], exact:true}).click();
    for (const [slug,label] of scenes) {
      await page.getByRole('button',{name:label,exact:true}).click();
      const issues = await page.evaluate(() => {
        const win=document.querySelector('#mod-window'), wr=win.getBoundingClientRect();
        const game=document.querySelector('#game').getBoundingClientRect();
        const failed=[];
        if(wr.right>game.right-345||wr.bottom>game.bottom-75) failed.push('window overlaps status HUD or bottom dock');
        for(const el of win.querySelectorAll('button,.slot,.row-heading strong,.state-tag,.mod-effect,.cost')) {
          const r=el.getBoundingClientRect();
          if(r.width===0)continue;
          if(r.left<wr.left||r.right>wr.right)failed.push(el.textContent+' overflows window');
          if(el.tagName==='BUTTON'&&r.bottom>wr.bottom)failed.push(el.textContent+' below window');
        }
        for(const cell of win.querySelectorAll('.module-cell')){
          const a=cell.querySelector('.row-heading').getBoundingClientRect(),b=cell.querySelector('.slot').getBoundingClientRect();
          if(a.bottom>b.top)failed.push('module status overlaps slot');
        }
        return failed;
      });
      check(issues.length===0,`${letter}/${slug}: geometry clear (${issues.join(', ')})`);
      if(slug==='cabin')check(await page.locator('#mod-window').getByText('牵引绳',{exact:true}).count()===0,`${letter}: cabin hides rope`);
      if(slug==='water')check(await page.locator('#mod-window').getByText('隔热棉',{exact:true}).count()===0,`${letter}: water hides insulation`);
      if(slug!=='water') {
        await page.locator('#game').screenshot({path:`output/playwright/climate-concepts/${letter}-${slug}.png`});
        await page.locator('#mod-window').screenshot({path:`output/playwright/climate-concepts/${letter}-${slug}-window.png`});
      }
      if(slug==='passage') {
        const hammer=page.getByRole('button',{name:'钢锤除冰',exact:false});
        const box=await hammer.boundingBox(), frame=await page.locator('#mod-window').boundingBox();
        check(box.y+box.height<frame.y+frame.height,`${letter}: deice action fully visible`);
        await hammer.click();
        check(await page.getByRole('button',{name:'无法通过 · 需要除冰',exact:true}).isDisabled(),`${letter}: exactly 100 still blocks travel`);
        await page.getByRole('button',{name:'钢锤除冰',exact:false}).click();
        check(await page.getByRole('button',{name:'前往珊瑚礁海域',exact:true}).isEnabled(),`${letter}: 50 unlocks travel`);
        await page.getByRole('button',{name:'重置演示',exact:true}).click();
      }
    }
    await page.locator('.ice-comparison').screenshot({path:`output/playwright/climate-concepts/${letter}-ice-states.png`});
  }
  await page.getByRole('button',{name:designs[0],exact:true}).click();
  await page.getByRole('button',{name:'舱室',exact:true}).click();
  await page.getByText('隔热棉 ×1',{exact:true}).dragTo(page.getByRole('button',{name:'隔热棉空槽，点击布置或拖入',exact:true}));
  check(await page.getByText('面积 15 · 隔热 3 级',{exact:true}).count()===1,'Drag install updates insulation level');
  await page.getByRole('button',{name:'拆除',exact:true}).click();
  check(await page.getByText('面积 15 · 隔热 2 级',{exact:true}).count()===1,'Remove returns original insulation state');
  await page.getByRole('button',{name:'双向通道',exact:true}).click();
  await page.getByRole('button',{name:'填充盐',exact:true}).click();
  check(await page.getByText('764 / 864',{exact:true}).count()===1,'Salt refill updates shared buoy durability');
  await page.getByRole('button',{name:'填充暖绒',exact:true}).click();
  check(await page.getByRole('button',{name:'填充盐',exact:true}).isDisabled(),'Full durability disables refill');
  await page.getByRole('button',{name:'重置演示',exact:true}).click();
  await page.getByRole('button',{name:'＋ 冰层季整体美术 实时霜冻 shader · 边缘 · 粒子',exact:true}).click();
  await page.getByRole('button',{name:'水域',exact:true}).click();
  for(const [slug,label] of [['normal','常温'],['onset','初临 · 第11天'],['severe','极寒 · 第17天'],['thaw','回暖 · 第29天']]){
    await page.getByRole('button',{name:label,exact:true}).click();
    await page.locator('#game').screenshot({path:`output/playwright/climate-concepts/season-${slug}.png`});
  }
  await page.getByRole('button',{name:'极寒 · 第17天',exact:true}).click();
  await page.getByRole('button',{name:'舱室',exact:true}).click();
  await page.locator('#game').screenshot({path:'output/playwright/climate-concepts/season-indoor.png'});
  const before=await page.locator('#weather').evaluate(el=>el.toDataURL());
  await page.waitForTimeout(400);
  const after=await page.locator('#weather').evaluate(el=>el.toDataURL());
  check(before!==after,'Particle animation changes over time');
  await page.getByRole('button',{name:'暂停动效',exact:true}).click();
  const paused=await page.locator('#weather').evaluate(el=>el.toDataURL());
  await page.waitForTimeout(150);
  check(paused===await page.locator('#weather').evaluate(el=>el.toDataURL()),'Pause freezes particle motion');
  await page.getByRole('checkbox',{name:'打开改装窗口',exact:true}).check();
  check(await page.locator('#mod-window').isVisible(),'Season overlay and modification window coexist');
  await page.locator('#game').screenshot({path:'output/playwright/climate-concepts/season-with-ui.png'});
  for (const width of [1280,760,430,375,320]){
    await page.setViewportSize({width,height:960});
    const overflow=await page.evaluate(()=>document.documentElement.scrollWidth>window.innerWidth+1);
    check(!overflow,`Review page fits ${width}px without horizontal overflow`);
  }
  check(errors.length===0,`No browser runtime errors (${errors.join(', ')})`);
  await page.setViewportSize({width:1600,height:1160});
  await page.getByRole('button',{name:designs[1],exact:true}).click();
  await page.getByRole('button',{name:'舱室',exact:true}).click();
  return {checks:checks.length,results:checks,errors};
}
