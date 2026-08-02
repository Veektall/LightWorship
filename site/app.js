const root = document.documentElement;
const body = document.body;
const reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
const finePointer = window.matchMedia('(pointer: fine)').matches;

const preloader = document.getElementById('preloader');
const loadProgress = document.getElementById('loadProgress');
const artifactViewport = document.getElementById('artifactViewport');
const artifactCanvas = document.getElementById('artifactCanvas');
const webglStatus = document.getElementById('webglStatus');
const localTime = document.getElementById('localTime');
const transitionBloom = document.getElementById('transitionBloom');
const exploreButton = document.getElementById('exploreButton');
const menuPanel = document.getElementById('menuPanel');
const menuButton = document.getElementById('menuButton');
const menuClose = document.getElementById('menuClose');
const header = document.querySelector('.site-header');

body.classList.add('is-loading');
let loaded3D = false;
let energized = false;
let rendererController = null;

function updateClock() {
  const time = new Intl.DateTimeFormat(undefined, {
    hour: '2-digit', minute: '2-digit', second: '2-digit', hour12: false
  }).format(new Date());
  localTime.textContent = `LOCAL — ${time}`;
}
updateClock();
setInterval(updateClock, 1000);

let fakeProgress = 8;
const progressTimer = setInterval(() => {
  fakeProgress = Math.min(fakeProgress + Math.random() * 12, loaded3D ? 100 : 88);
  loadProgress.style.width = `${fakeProgress}%`;
  if (fakeProgress >= 100) clearInterval(progressTimer);
}, 120);

function finishLoading() {
  if (preloader.classList.contains('is-done')) return;
  loadProgress.style.width = '100%';
  setTimeout(() => {
    preloader.classList.add('is-done');
    body.classList.remove('is-loading');
    document.querySelectorAll('[data-reveal]').forEach((element, index) => {
      setTimeout(() => element.classList.add('is-visible'), reducedMotion ? 0 : 80 + index * 65);
    });
  }, 260);
}

setTimeout(finishLoading, 2600);

function setMenu(open) {
  menuPanel.classList.toggle('is-open', open);
  body.classList.toggle('menu-open', open);
  menuPanel.setAttribute('aria-hidden', String(!open));
  menuButton.setAttribute('aria-expanded', String(open));
  if (open) menuClose.focus();
}
menuButton.addEventListener('click', () => setMenu(true));
menuClose.addEventListener('click', () => setMenu(false));
menuPanel.querySelectorAll('a').forEach(link => link.addEventListener('click', () => setMenu(false)));
document.addEventListener('keydown', event => {
  if (event.key === 'Escape') setMenu(false);
});

const revealObserver = new IntersectionObserver(entries => {
  entries.forEach(entry => {
    if (entry.isIntersecting) {
      entry.target.classList.add('is-visible');
      revealObserver.unobserve(entry.target);
    }
  });
}, { threshold: 0.16 });
document.querySelectorAll('.reveal-on-scroll').forEach(element => revealObserver.observe(element));

function updateScroll() {
  const y = window.scrollY;
  const heroHeight = document.getElementById('hero').offsetHeight;
  const compression = Math.min(y / Math.max(heroHeight * 0.8, 1), 1);
  root.style.setProperty('--scroll-compress', compression.toFixed(3));
  header.classList.toggle('is-scrolled', y > 28);
  rendererController?.setScroll(compression);
}
window.addEventListener('scroll', updateScroll, { passive: true });
updateScroll();

let pointerTarget = { x: 0, y: 0 };
let pointerSmooth = { x: 0, y: 0 };
window.addEventListener('pointermove', event => {
  pointerTarget.x = (event.clientX / window.innerWidth - 0.5) * 2;
  pointerTarget.y = (event.clientY / window.innerHeight - 0.5) * 2;
  if (finePointer) body.classList.add('has-pointer');
}, { passive: true });

if (finePointer) {
  const dot = document.getElementById('cursorDot');
  const ring = document.getElementById('cursorRing');
  let dotX = innerWidth / 2, dotY = innerHeight / 2;
  let ringX = dotX, ringY = dotY;
  window.addEventListener('pointermove', event => { dotX = event.clientX; dotY = event.clientY; }, { passive: true });
  const tickCursor = () => {
    ringX += (dotX - ringX) * 0.15;
    ringY += (dotY - ringY) * 0.15;
    dot.style.transform = `translate3d(${dotX}px,${dotY}px,0)`;
    ring.style.transform = `translate3d(${ringX}px,${ringY}px,0)`;
    requestAnimationFrame(tickCursor);
  };
  tickCursor();
  document.querySelectorAll('.interactive').forEach(element => {
    element.addEventListener('pointerenter', () => body.classList.add('cursor-active'));
    element.addEventListener('pointerleave', () => body.classList.remove('cursor-active'));
  });
}

function pointerLoop() {
  pointerSmooth.x += (pointerTarget.x - pointerSmooth.x) * 0.045;
  pointerSmooth.y += (pointerTarget.y - pointerSmooth.y) * 0.045;
  if (!reducedMotion) {
    root.style.setProperty('--mx', pointerSmooth.x.toFixed(3));
    root.style.setProperty('--my', pointerSmooth.y.toFixed(3));
  }
  rendererController?.setPointer(pointerSmooth.x, pointerSmooth.y);
  requestAnimationFrame(pointerLoop);
}
pointerLoop();

function setEnergy(active) {
  energized = active;
  artifactViewport.classList.toggle('is-energized', active);
  rendererController?.setEnergy(active);
}
exploreButton.addEventListener('pointerenter', () => setEnergy(true));
exploreButton.addEventListener('pointerleave', () => setEnergy(false));
exploreButton.addEventListener('focus', () => setEnergy(true));
exploreButton.addEventListener('blur', () => setEnergy(false));
exploreButton.addEventListener('click', () => {
  setEnergy(true);
  rendererController?.dolly();
  transitionBloom.classList.remove('is-active');
  void transitionBloom.offsetWidth;
  transitionBloom.classList.add('is-active');
  setTimeout(() => document.getElementById('archive').scrollIntoView({ behavior: reducedMotion ? 'auto' : 'smooth' }), 530);
  setTimeout(() => setEnergy(false), 1450);
});

async function initArtifact() {
  try {
    const THREE = await import('https://cdn.jsdelivr.net/npm/three@0.169.0/build/three.module.js');
    const scene = new THREE.Scene();
    const camera = new THREE.PerspectiveCamera(28, 1, 0.1, 100);
    camera.position.set(0, 0, 5.4);

    const renderer = new THREE.WebGLRenderer({ canvas: artifactCanvas, alpha: true, antialias: true, powerPreference: 'high-performance' });
    renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 1.5));
    renderer.setClearColor(0x000000, 0);
    renderer.outputColorSpace = THREE.SRGBColorSpace;
    renderer.toneMapping = THREE.ACESFilmicToneMapping;
    renderer.toneMappingExposure = 1.05;

    const artifact = new THREE.Group();
    scene.add(artifact);

    const innerRig = new THREE.Group();
    artifact.add(innerRig);

    const obsidian = new THREE.MeshPhysicalMaterial({
      color: 0x100c17,
      roughness: 0.19,
      metalness: 0.32,
      transmission: 0.1,
      thickness: 1.1,
      clearcoat: 1,
      clearcoatRoughness: 0.12,
      emissive: 0x2d1057,
      emissiveIntensity: 0.32
    });
    const coreMesh = new THREE.Mesh(new THREE.IcosahedronGeometry(0.72, 3), obsidian);
    coreMesh.scale.set(1.05, 0.96, 1.02);
    innerRig.add(coreMesh);

    const fractureMaterial = new THREE.MeshBasicMaterial({ color: 0xb7a0f8, wireframe: true, transparent: true, opacity: 0.11, blending: THREE.AdditiveBlending, depthWrite: false });
    const fracture = new THREE.Mesh(new THREE.IcosahedronGeometry(0.78, 2), fractureMaterial);
    fracture.rotation.set(0.4, 0.2, -0.18);
    innerRig.add(fracture);

    const glowMaterial = new THREE.MeshBasicMaterial({ color: 0x8b5cf6, transparent: true, opacity: 0.28, blending: THREE.AdditiveBlending, depthWrite: false });
    const glowCore = new THREE.Mesh(new THREE.SphereGeometry(0.5, 48, 48), glowMaterial);
    innerRig.add(glowCore);
    const hotCore = new THREE.Mesh(new THREE.SphereGeometry(0.25, 32, 32), new THREE.MeshBasicMaterial({ color: 0xe9ddff, transparent: true, opacity: 0.72, blending: THREE.AdditiveBlending, depthWrite: false }));
    innerRig.add(hotCore);

    const coreLight = new THREE.PointLight(0x9c6dff, 12, 7, 2);
    innerRig.add(coreLight);

    const ringMaterial = new THREE.MeshStandardMaterial({ color: 0x8e7bac, metalness: 0.94, roughness: 0.22, emissive: 0x2f155b, emissiveIntensity: 0.22 });
    const brassMaterial = new THREE.MeshStandardMaterial({ color: 0xb69a63, metalness: 0.95, roughness: 0.28, emissive: 0x2d2414, emissiveIntensity: 0.15 });
    const rings = [];
    const ringSpecs = [
      [1.13, 0.022, [1.05, 0.18, 0.2]],
      [1.44, 0.018, [0.28, 1.08, -0.46]],
      [1.74, 0.013, [0.78, -0.62, 0.92]]
    ];
    ringSpecs.forEach(([radius, tube, rotation], index) => {
      const ring = new THREE.Mesh(new THREE.TorusGeometry(radius, tube, 12, 160), index === 1 ? brassMaterial : ringMaterial);
      ring.rotation.set(...rotation);
      ring.userData.speed = [0.13, -0.09, 0.055][index];
      artifact.add(ring);
      rings.push(ring);
    });

    const ticks = new THREE.Group();
    for (let i = 0; i < 28; i++) {
      const a = (i / 28) * Math.PI * 2;
      const tick = new THREE.Mesh(new THREE.BoxGeometry(i % 7 === 0 ? 0.05 : 0.025, i % 7 === 0 ? 0.13 : 0.07, 0.018), i % 7 === 0 ? brassMaterial : ringMaterial);
      tick.position.set(Math.cos(a) * 1.74, Math.sin(a) * 1.74, 0);
      tick.rotation.z = a;
      ticks.add(tick);
    }
    ticks.rotation.set(0.78, -0.62, 0.92);
    artifact.add(ticks);

    const shardMaterial = new THREE.MeshPhysicalMaterial({ color: 0x241a35, metalness: 0.55, roughness: 0.25, clearcoat: 0.8, emissive: 0x32155c, emissiveIntensity: 0.28 });
    const shards = [];
    const seeded = (index) => {
      const x = Math.sin(index * 127.1) * 43758.5453;
      return x - Math.floor(x);
    };
    for (let i = 0; i < 18; i++) {
      const shard = new THREE.Mesh(new THREE.TetrahedronGeometry(0.055 + seeded(i) * 0.09, 0), shardMaterial);
      const angle = seeded(i + 4) * Math.PI * 2;
      const radius = 0.92 + seeded(i + 8) * 0.9;
      shard.position.set(Math.cos(angle) * radius, Math.sin(angle) * radius * 0.72, (seeded(i + 12) - 0.5) * 1.2);
      shard.rotation.set(seeded(i + 2) * 3, seeded(i + 3) * 3, seeded(i + 5) * 3);
      shard.userData = { baseY: shard.position.y, phase: seeded(i + 17) * Math.PI * 2, speed: 0.35 + seeded(i + 19) * 0.5 };
      artifact.add(shard);
      shards.push(shard);
    }

    const particleCount = 520;
    const positions = new Float32Array(particleCount * 3);
    for (let i = 0; i < particleCount; i++) {
      const theta = seeded(i + 30) * Math.PI * 2;
      const phi = Math.acos(2 * seeded(i + 60) - 1);
      const r = 1.75 + seeded(i + 90) * 0.65;
      positions[i * 3] = r * Math.sin(phi) * Math.cos(theta);
      positions[i * 3 + 1] = r * Math.sin(phi) * Math.sin(theta);
      positions[i * 3 + 2] = r * Math.cos(phi) * 0.52;
    }
    const particleGeometry = new THREE.BufferGeometry();
    particleGeometry.setAttribute('position', new THREE.BufferAttribute(positions, 3));
    const particles = new THREE.Points(particleGeometry, new THREE.PointsMaterial({ color: 0xb69cff, size: 0.014, transparent: true, opacity: 0.58, blending: THREE.AdditiveBlending, depthWrite: false, sizeAttenuation: true }));
    artifact.add(particles);

    scene.add(new THREE.AmbientLight(0x8d78ae, 0.55));
    const key = new THREE.DirectionalLight(0xd9cdff, 4.3); key.position.set(3, 4, 5); scene.add(key);
    const rim = new THREE.DirectionalLight(0x6730c7, 5.5); rim.position.set(-4, -1, -3); scene.add(rim);
    const warm = new THREE.PointLight(0xc7a567, 2.5, 8); warm.position.set(2.4, -1.7, 2.5); scene.add(warm);

    let pointer = { x: 0, y: 0 };
    let scroll = 0;
    let energyTarget = 0;
    let energyValue = 0;
    let dollyValue = 0;
    let lastTime = performance.now();
    let active = true;

    function resize() {
      const rect = artifactViewport.getBoundingClientRect();
      const width = Math.max(1, rect.width * 1.22);
      const height = Math.max(1, rect.height * 1.22);
      renderer.setSize(width, height, false);
      camera.aspect = width / height;
      camera.updateProjectionMatrix();
    }
    resize();
    window.addEventListener('resize', resize);
    document.addEventListener('visibilitychange', () => { active = !document.hidden; });

    rendererController = {
      setPointer(x, y) { pointer.x = x; pointer.y = y; },
      setScroll(value) { scroll = value; },
      setEnergy(value) { energyTarget = value ? 1 : 0; },
      dolly() { dollyValue = 1; }
    };

    function animate(now) {
      if (!active) return;
      const dt = Math.min((now - lastTime) / 1000, 0.05);
      lastTime = now;
      const t = now / 1000;
      energyValue += (energyTarget - energyValue) * Math.min(1, dt * 5);
      dollyValue += (0 - dollyValue) * Math.min(1, dt * 1.35);

      const targetRX = reducedMotion ? 0 : pointer.y * 0.075;
      const targetRY = reducedMotion ? 0 : pointer.x * 0.095;
      artifact.rotation.x += (targetRX - artifact.rotation.x) * Math.min(1, dt * 2.4);
      artifact.rotation.y += (targetRY - artifact.rotation.y) * Math.min(1, dt * 2.4);
      artifact.rotation.z = Math.sin(t * 0.22) * 0.025;
      artifact.scale.setScalar(1 - scroll * 0.08 + dollyValue * 0.36);

      rings.forEach((ring, index) => {
        ring.rotation.z += ring.userData.speed * dt * (1 + energyValue * 2.8);
        if (index === 1) ring.rotation.x += 0.018 * dt;
      });
      ticks.rotation.z -= 0.055 * dt * (1 + energyValue * 2.2);
      particles.rotation.y += 0.025 * dt * (1 + energyValue * 1.8);
      particles.rotation.z = Math.sin(t * 0.13) * 0.08;
      innerRig.rotation.y -= 0.08 * dt;
      innerRig.rotation.x = Math.sin(t * 0.33) * 0.045;
      const breath = 1 + Math.sin(t * 1.35) * 0.035 + energyValue * 0.14;
      glowCore.scale.setScalar(breath);
      hotCore.scale.setScalar(0.95 + Math.sin(t * 1.9) * 0.08 + energyValue * 0.18);
      glowMaterial.opacity = 0.25 + Math.sin(t * 1.25) * 0.055 + energyValue * 0.24;
      coreLight.intensity = 10 + Math.sin(t * 1.3) * 2 + energyValue * 11;
      obsidian.emissiveIntensity = 0.26 + energyValue * 0.55;
      ringMaterial.emissiveIntensity = 0.18 + energyValue * 0.4;
      shards.forEach(shard => {
        shard.position.y = shard.userData.baseY + Math.sin(t * shard.userData.speed + shard.userData.phase) * 0.07;
        shard.rotation.x += dt * 0.15;
        shard.rotation.y -= dt * 0.12;
      });
      camera.position.z = 5.4 - dollyValue * 1.55 + scroll * 0.25;
      renderer.render(scene, camera);
    }
    renderer.setAnimationLoop(animate);

    body.classList.add('webgl-ready');
    webglStatus.textContent = 'WEBGL — ACTIVE';
    loaded3D = true;
    finishLoading();
  } catch (error) {
    console.warn('NOCTURNE WebGL fallback active:', error);
    webglStatus.textContent = 'CSS ORBIT — ACTIVE';
    loaded3D = true;
    finishLoading();
  }
}

initArtifact();
