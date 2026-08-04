import * as THREE from 'three';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';
import { MOUSE } from 'three';
import {
  normalizeStateAppearance,
  isSolidFill,
  createHazardMaterial,
  updateHazardMaterialOpacity,
  setHazardMaterialSelection,
  edgeColorForAppearance,
} from './surface-wraps.js';
import { isFpsBindingDown, DEFAULT_FPS_KEYS, DEFAULT_FPS_SPRINT_MULTIPLIER } from './viewer-options.js';

function hexToThreeInt(hex) {
  const h = String(hex || '#888888').trim();
  if (!/^#[0-9A-Fa-f]{6}$/.test(h)) return 0x888888;
  return parseInt(h.slice(1), 16);
}

function darkenColorInt(rgbInt, factor) {
  const r = ((rgbInt >> 16) & 0xff) * factor;
  const g = ((rgbInt >> 8) & 0xff) * factor;
  const b = (rgbInt & 0xff) * factor;
  return (Math.round(r) << 16) | (Math.round(g) << 8) | Math.round(b);
}

function shortLabel(surfaceNumber) {
  const s = String(surfaceNumber || '');
  const dash = s.lastIndexOf('-');
  const suffix = dash >= 0 ? s.slice(dash + 1) : s;
  if (suffix.length <= 4) return suffix.padStart(4, '0');
  return suffix.slice(-4);
}

function computeBounds(group) {
  const box = new THREE.Box3().setFromObject(group);
  const size = box.getSize(new THREE.Vector3());
  const center = box.getCenter(new THREE.Vector3());
  return { box, size, center };
}

function largestFaceAxis(size) {
  const faces = [
    { axis: 'x', area: size.y * size.z },
    { axis: 'y', area: size.x * size.z },
    { axis: 'z', area: size.x * size.y },
  ];
  faces.sort((a, b) => b.area - a.area);
  return faces[0];
}

function hexToRgbaCss(hex, alpha = 1) {
  const h = String(hex || '#000000').replace('#', '');
  if (h.length !== 6) return `rgba(15, 23, 42, ${alpha})`;
  const r = parseInt(h.slice(0, 2), 16);
  const g = parseInt(h.slice(2, 4), 16);
  const b = parseInt(h.slice(4, 6), 16);
  return `rgba(${r}, ${g}, ${b}, ${alpha})`;
}

function createStickerTexture(text, stickerOptions = {}) {
  const px = 512;
  const fontFamily = stickerOptions.fontFamily || '"Segoe UI", system-ui, sans-serif';
  const textColor = stickerOptions.textColor || '#f8fafc';
  const backgroundColor = stickerOptions.backgroundColor || '#0f172a';
  const borderColor = stickerOptions.borderColor || '#94a3b8';
  const canvas = document.createElement('canvas');
  canvas.width = px;
  canvas.height = px;
  const ctx = canvas.getContext('2d');
  ctx.clearRect(0, 0, px, px);
  const pad = Math.round(px * 0.08);
  const r = Math.round(px * 0.06);
  ctx.fillStyle = hexToRgbaCss(backgroundColor, 0.94);
  ctx.strokeStyle = hexToRgbaCss(borderColor, 0.75);
  ctx.lineWidth = Math.max(2, px * 0.012);
  if (typeof ctx.roundRect === 'function') {
    ctx.beginPath();
    ctx.roundRect(pad, pad, px - pad * 2, px - pad * 2, r);
    ctx.fill();
    ctx.stroke();
  } else {
    ctx.fillRect(pad, pad, px - pad * 2, px - pad * 2);
    ctx.strokeRect(pad, pad, px - pad * 2, px - pad * 2);
  }
  ctx.fillStyle = textColor;
  ctx.font = `bold ${Math.round(px * 0.34)}px ${fontFamily}`;
  ctx.textAlign = 'center';
  ctx.textBaseline = 'middle';
  ctx.fillText(text, px / 2, px / 2 + 1);
  const texture = new THREE.CanvasTexture(canvas);
  texture.minFilter = THREE.LinearFilter;
  texture.magFilter = THREE.LinearFilter;
  texture.generateMipmaps = false;
  texture.needsUpdate = true;
  return texture;
}

function addFaceStickers(group, labelText, bounds, stickerOptions = {}) {
  const { box, size } = bounds;
  const face = largestFaceAxis(size);
  const label = shortLabel(labelText);
  const texture = createStickerTexture(label, stickerOptions);
  const faceW = face.axis === 'x' ? size.y : size.x;
  const faceH = face.axis === 'y' ? size.z : face.axis === 'x' ? size.z : size.y;
  const stickerSize = Math.min(Math.max(Math.min(faceW, faceH) * 0.2, 5), 14);
  const offset = 0.03;

  const makeSticker = (position, normal) => {
    const mat = new THREE.MeshBasicMaterial({
      map: texture,
      transparent: true,
      depthTest: true,
      depthWrite: false,
      polygonOffset: true,
      polygonOffsetFactor: -2,
      polygonOffsetUnits: -2,
      side: THREE.FrontSide,
    });
    const mesh = new THREE.Mesh(new THREE.PlaneGeometry(stickerSize, stickerSize), mat);
    mesh.position.copy(position);
    const target = position.clone().add(normal);
    mesh.lookAt(target);
    mesh.renderOrder = 2;
    mesh.userData.isSticker = true;
    return mesh;
  };

  if (face.axis === 'x') {
    group.add(
      makeSticker(new THREE.Vector3(box.min.x - offset, bounds.center.y, bounds.center.z), new THREE.Vector3(-1, 0, 0)),
      makeSticker(new THREE.Vector3(box.max.x + offset, bounds.center.y, bounds.center.z), new THREE.Vector3(1, 0, 0))
    );
  } else if (face.axis === 'y') {
    group.add(
      makeSticker(new THREE.Vector3(bounds.center.x, box.min.y - offset, bounds.center.z), new THREE.Vector3(0, -1, 0)),
      makeSticker(new THREE.Vector3(bounds.center.x, box.max.y + offset, bounds.center.z), new THREE.Vector3(0, 1, 0))
    );
  } else {
    group.add(
      makeSticker(new THREE.Vector3(bounds.center.x, bounds.center.y, box.min.z - offset), new THREE.Vector3(0, 0, -1)),
      makeSticker(new THREE.Vector3(bounds.center.x, bounds.center.y, box.max.z + offset), new THREE.Vector3(0, 0, 1))
    );
  }
}

function applySolidMaterial(mat, appearance) {
  const { color } = normalizeStateAppearance(appearance);
  mat.color.setHex(hexToThreeInt(color));
  mat.needsUpdate = true;
}

function createSurfaceMeshMaterial(appearance, surfaceOpacity) {
  const { fillType } = normalizeStateAppearance(appearance);
  if (isSolidFill(fillType)) {
    const mat = new THREE.MeshStandardMaterial({
      metalness: 0.12,
      roughness: 0.52,
    });
    applySolidMaterial(mat, appearance);
    applyMeshOpacity(mat, surfaceOpacity);
    return { mat, isHazard: false };
  }
  const mat = createHazardMaterial(fillType, THREE, surfaceOpacity);
  if (!mat) {
    const fallback = new THREE.MeshStandardMaterial({
      metalness: 0.12,
      roughness: 0.52,
    });
    applySolidMaterial(fallback, appearance);
    applyMeshOpacity(fallback, surfaceOpacity);
    return { mat: fallback, isHazard: false };
  }
  return { mat, isHazard: true };
}

function syncMeshMaterial(mesh, appearance, surfaceOpacity) {
  const { fillType } = normalizeStateAppearance(appearance);
  const wantHazard = !isSolidFill(fillType);
  const hasHazard = Boolean(mesh.userData.isHazardMaterial);

  if (wantHazard !== hasHazard) {
    if (mesh.material) mesh.material.dispose();
    const created = createSurfaceMeshMaterial(appearance, surfaceOpacity);
    mesh.material = created.mat;
    mesh.userData.isHazardMaterial = created.isHazard;
    mesh.userData.hazardFillType = wantHazard ? fillType : null;
    mesh.material.polygonOffset = !created.isHazard;
    if (!created.isHazard) {
      mesh.material.polygonOffsetFactor = 1;
      mesh.material.polygonOffsetUnits = 1;
    }
    return;
  }

  if (wantHazard) {
    if (mesh.userData.hazardFillType !== fillType) {
      mesh.material.dispose();
      mesh.material = createHazardMaterial(fillType, THREE, surfaceOpacity);
      mesh.userData.hazardFillType = fillType;
    } else {
      updateHazardMaterialOpacity(mesh.material, surfaceOpacity);
    }
  } else {
    applySolidMaterial(mesh.material, appearance);
    applyMeshOpacity(mesh.material, surfaceOpacity);
  }
}

function applyMeshOpacity(mat, opacity) {
  const value = Math.min(1, Math.max(0.25, opacity));
  const opaque = value >= 0.999;
  mat.opacity = value;
  mat.transparent = !opaque;
  mat.depthWrite = opaque;
  mat.needsUpdate = true;
}

function isTypingTarget(el) {
  if (!el) return false;
  if (el.closest?.('dialog[open]')) return true;
  const tag = el.tagName;
  return tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT' || el.isContentEditable;
}

function isModalDialogOpen() {
  return Boolean(document.querySelector('dialog[open]'));
}

function orbitActionForName(action) {
  switch (action) {
    case 'rotate':
      return MOUSE.ROTATE;
    case 'pan':
      return MOUSE.PAN;
    case 'zoom':
      return MOUSE.DOLLY;
    default:
      return null;
  }
}

function applyOrbitMouseButtons(controls, viewerOptions) {
  if (!controls || !viewerOptions?.mouseButtons) return;
  const mb = viewerOptions.mouseButtons;
  const actionForButton = { 0: 'none', 1: 'none', 2: 'none' };
  actionForButton[mb.rotate] = 'rotate';
  actionForButton[mb.pan] = 'pan';
  actionForButton[mb.zoom] = 'zoom';
  controls.mouseButtons = {
    LEFT: orbitActionForName(actionForButton[0]) ?? MOUSE.ROTATE,
    MIDDLE: orbitActionForName(actionForButton[1]) ?? MOUSE.DOLLY,
    RIGHT: orbitActionForName(actionForButton[2]) ?? MOUSE.PAN,
  };
}

function expandBoxFromSurfaceBoxes(target, surface) {
  for (const box of surface.boxes || []) {
    target.expandByPoint(new THREE.Vector3(box.x, box.y, box.z));
    target.expandByPoint(new THREE.Vector3(box.x + box.xLength, box.y + box.yLength, box.z + box.zLength));
  }
}

export class UnitViewer3d {
  constructor(hostEl) {
    this.hostEl = hostEl;
    this.scene = null;
    this.camera = null;
    this.renderer = null;
    this.controls = null;
    this.root = null;
    this.grid = null;
    this.raycaster = new THREE.Raycaster();
    this.pointer = new THREE.Vector2();
    this.surfaceGroups = new Map();
    this.selectedSurfaceNumber = null;
    this.onSurfacePick = null;
    this.onSurfaceHide = null;
    this.onSurfaceInfoRequest = null;
    this.surfaceOpacity = 0.9;
    this.viewerOptions = {
      showGrid: true,
      fpsControlsEnabled: true,
      mouseButtons: { rotate: 0, pan: 2, zoom: 1 },
      fpsKeys: { ...DEFAULT_FPS_KEYS },
    };
    this.loadedSurfaces = [];
    this.sceneScale = 100;
    this.lastRightClick = { time: 0, surfaceNumber: null };
    this.lastLeftClick = { time: 0, surfaceNumber: null };
    this.pointerHeldButtons = new Set();
    this.rafId = null;
    this.lastFrameTime = performance.now();
    this.pressedKeyCodes = new Set();
    this.boundResize = () => this.resize();
    this.boundPointerDown = (e) => this.onPointerDown(e);
    this.boundPointerUp = (e) => this.onPointerUp(e);
    this.boundPointerLeave = () => this.pointerHeldButtons.clear();
    this.boundContextMenu = (e) => e.preventDefault();
    this.boundKeyDown = (e) => this.onKeyDown(e);
    this.boundKeyUp = (e) => this.onKeyUp(e);
    this._moveForward = new THREE.Vector3();
    this._moveRight = new THREE.Vector3();
    this._moveDelta = new THREE.Vector3();
  }

  init() {
    if (this.renderer) return;
    this.hostEl.innerHTML = '';
    const rect = this.hostEl.getBoundingClientRect();
    const w = Math.max(2, rect.width);
    const h = Math.max(2, rect.height);

    this.scene = new THREE.Scene();
    this.scene.background = new THREE.Color(0x020617);

    this.root = new THREE.Group();
    this.scene.add(this.root);

    this.camera = new THREE.PerspectiveCamera(50, w / h, 0.1, 1e7);
    this.camera.up.set(0, 1, 0);

    const canvas = document.createElement('canvas');
    canvas.style.display = 'block';
    canvas.tabIndex = 0;
    this.hostEl.appendChild(canvas);

    this.renderer = new THREE.WebGLRenderer({ canvas, antialias: true, logarithmicDepthBuffer: true });
    this.renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
    this.renderer.setSize(w, h);
    this.renderer.sortObjects = true;

    this.controls = new OrbitControls(this.camera, canvas);
    this.controls.enableDamping = true;
    this.controls.dampingFactor = 0.08;
    applyOrbitMouseButtons(this.controls, this.viewerOptions);

    const ambient = new THREE.AmbientLight(0xffffff, 0.48);
    this.scene.add(ambient);
    const key = new THREE.DirectionalLight(0xffffff, 0.92);
    key.position.set(5, 10, 7);
    this.scene.add(key);
    const fill = new THREE.DirectionalLight(0xb8c5e0, 0.32);
    fill.position.set(-5, 4, -6);
    this.scene.add(fill);

    window.addEventListener('resize', this.boundResize);
    window.addEventListener('keydown', this.boundKeyDown);
    window.addEventListener('keyup', this.boundKeyUp);
    canvas.addEventListener('pointerdown', this.boundPointerDown);
    canvas.addEventListener('pointerup', this.boundPointerUp);
    canvas.addEventListener('pointerleave', this.boundPointerLeave);
    canvas.addEventListener('contextmenu', this.boundContextMenu);

    this.animate();
  }

  dispose() {
    window.removeEventListener('resize', this.boundResize);
    window.removeEventListener('keydown', this.boundKeyDown);
    window.removeEventListener('keyup', this.boundKeyUp);
    if (this.renderer) {
      this.renderer.domElement.removeEventListener('pointerdown', this.boundPointerDown);
      this.renderer.domElement.removeEventListener('pointerup', this.boundPointerUp);
      this.renderer.domElement.removeEventListener('pointerleave', this.boundPointerLeave);
      this.renderer.domElement.removeEventListener('contextmenu', this.boundContextMenu);
    }
    if (this.rafId) cancelAnimationFrame(this.rafId);
    this.clearSurfaces();
    if (this.renderer) {
      this.renderer.dispose();
      this.renderer = null;
    }
    this.controls = null;
    this.scene = null;
    this.camera = null;
    this.hostEl.innerHTML = '';
  }

  onKeyDown(event) {
    if (isModalDialogOpen()) return;
    if (isTypingTarget(document.activeElement) || isTypingTarget(event.target)) return;
    if (event.ctrlKey && !event.metaKey && event.code === 'KeyW') {
      event.preventDefault();
    }
    if (event.code === 'Space') event.preventDefault();
    this.setKeyState(event.code, true, event);
  }

  onKeyUp(event) {
    if (isModalDialogOpen()) return;
    if (isTypingTarget(document.activeElement) || isTypingTarget(event.target)) return;
    this.setKeyState(event.code, false, event);
  }

  setKeyState(code, pressed) {
    if (pressed) this.pressedKeyCodes.add(code);
    else this.pressedKeyCodes.delete(code);
  }

  isKeyDown(binding) {
    return isFpsBindingDown(binding, this.pressedKeyCodes);
  }

  onPointerUp(event) {
    this.pointerHeldButtons.delete(event.button);
  }

  setViewerOptions(viewerOptions) {
    this.viewerOptions = viewerOptions || this.viewerOptions;
    applyOrbitMouseButtons(this.controls, this.viewerOptions);
    this.updateGrid(this.loadedSurfaces);
  }

  updateFpsMovement(dt) {
    if (!this.camera || !this.controls || !this.viewerOptions.fpsControlsEnabled) return;
    const fpsKeys = this.viewerOptions.fpsKeys || DEFAULT_FPS_KEYS;
    const w = this.isKeyDown('KeyW');
    const a = this.isKeyDown('KeyA');
    const s = this.isKeyDown('KeyS');
    const d = this.isKeyDown('KeyD');
    const ascend = this.isKeyDown(fpsKeys.ascend);
    const descend = this.isKeyDown(fpsKeys.descend);
    if (!w && !a && !s && !d && !ascend && !descend) return;

    const sprint = this.isKeyDown(fpsKeys.sprint);
    const sprintMult = this.viewerOptions.fpsSprintMultiplier ?? DEFAULT_FPS_SPRINT_MULTIPLIER;
    const speed = this.sceneScale * 0.45 * dt * (sprint ? sprintMult : 1);
    this._moveDelta.set(0, 0, 0);

    this.camera.getWorldDirection(this._moveForward);
    this._moveForward.y = 0;
    if (this._moveForward.lengthSq() > 1e-8) this._moveForward.normalize();
    else this._moveForward.set(0, 0, -1);

    this._moveRight.crossVectors(this._moveForward, this.camera.up).normalize();

    if (w) this._moveDelta.add(this._moveForward);
    if (s) this._moveDelta.sub(this._moveForward);
    if (d) this._moveDelta.add(this._moveRight);
    if (a) this._moveDelta.sub(this._moveRight);
    if (ascend) this._moveDelta.y += 1;
    if (descend) this._moveDelta.y -= 1;

    if (this._moveDelta.lengthSq() === 0) return;
    this._moveDelta.normalize().multiplyScalar(speed);

    this.camera.position.add(this._moveDelta);
    this.controls.target.add(this._moveDelta);
  }

  animate() {
    this.rafId = requestAnimationFrame(() => this.animate());
    if (!this.controls || !this.renderer) return;
    const now = performance.now();
    const dt = Math.min(0.05, (now - this.lastFrameTime) / 1000);
    this.lastFrameTime = now;
    this.updateFpsMovement(dt);
    this.controls.update();
    this.renderer.render(this.scene, this.camera);
  }

  resize() {
    if (!this.renderer || !this.camera) return;
    const rect = this.hostEl.getBoundingClientRect();
    const w = Math.max(2, rect.width);
    const h = Math.max(2, rect.height);
    this.camera.aspect = w / h;
    this.camera.updateProjectionMatrix();
    this.renderer.setSize(w, h);
  }

  removeGrid() {
    if (!this.grid) return;
    this.root.remove(this.grid);
    this.grid.geometry.dispose();
    this.grid.material.dispose();
    this.grid = null;
  }

  computeGridSize(surfaces, boundsBox) {
    const size = boundsBox.getSize(new THREE.Vector3());
    let skidFootprint = 0;
    if (surfaces && surfaces.length) {
      const skidBoxes = new Map();
      for (const surface of surfaces) {
        const skid = surface.skidId ?? 1;
        if (!skidBoxes.has(skid)) skidBoxes.set(skid, new THREE.Box3());
        expandBoxFromSurfaceBoxes(skidBoxes.get(skid), surface);
      }
      const union = new THREE.Box3();
      for (const box of skidBoxes.values()) {
        if (!box.isEmpty()) union.union(box);
      }
      if (!union.isEmpty()) {
        const skidSize = union.getSize(new THREE.Vector3());
        skidFootprint = Math.max(skidSize.x, skidSize.z);
      }
    }
    const baseDim = Math.max(size.x, size.y, size.z, skidFootprint, 1);
    return Math.max(baseDim * 4.8, skidFootprint * 2.5, this.sceneScale * 3.8);
  }

  updateGrid(surfaces) {
    if (!this.root) return;
    if (!this.viewerOptions.showGrid) {
      this.removeGrid();
      return;
    }
    const box = this.getSurfaceBoundsBox();
    if (box.isEmpty()) {
      this.removeGrid();
      return;
    }

    const gridSize = this.computeGridSize(surfaces, box);
    const gridDivs = Math.min(64, Math.max(16, Math.round(gridSize / Math.max(this.sceneScale * 0.12, 0.5))));

    if (this.grid) {
      this.root.remove(this.grid);
      this.grid.geometry.dispose();
      this.grid.material.dispose();
      this.grid = null;
    }
    this.grid = new THREE.GridHelper(gridSize, gridDivs, 0x64748b, 0x1e293b);
    this.grid.position.y = box.min.y;
    this.root.add(this.grid);
  }

  clearSurfaces() {
    for (const group of this.surfaceGroups.values()) {
      this.root.remove(group);
      group.traverse((obj) => {
        if (obj.geometry) obj.geometry.dispose();
        if (obj.material) {
          const m = obj.material;
          if (Array.isArray(m)) m.forEach((x) => x.dispose());
          else {
            if (m.map) m.map.dispose();
            m.dispose();
          }
        }
      });
    }
    this.surfaceGroups.clear();
    this.removeGrid();
  }

  getSurfaceBoundsBox() {
    const box = new THREE.Box3();
    let any = false;
    for (const group of this.surfaceGroups.values()) {
      if (!group.visible) continue;
      box.expandByObject(group);
      any = true;
    }
    if (!any) {
      for (const group of this.surfaceGroups.values()) {
        box.expandByObject(group);
      }
    }
    return box;
  }

  buildSurfaces(surfaces, getSurfaceAppearance, isHidden = () => false, surfaceOpacity = 0.9, viewerOptions = null) {
    this.init();
    this.surfaceOpacity = surfaceOpacity;
    this.loadedSurfaces = surfaces || [];
    if (viewerOptions) this.viewerOptions = viewerOptions;
    this.clearSurfaces();

    for (const surface of surfaces) {
      const group = new THREE.Group();
      group.userData.surfaceNumber = surface.surfaceNumber;
      const appearance = getSurfaceAppearance(surface.surfaceNumber);
      const edgeColor = edgeColorForAppearance(appearance, hexToThreeInt, darkenColorInt);

      for (const box of surface.boxes) {
        const cx = box.x + box.xLength / 2;
        const cy = box.y + box.yLength / 2;
        const cz = box.z + box.zLength / 2;
        const geom = new THREE.BoxGeometry(box.xLength, box.yLength, box.zLength);

        const mat = createSurfaceMeshMaterial(appearance, this.surfaceOpacity);
        const mesh = new THREE.Mesh(geom, mat.mat);
        mesh.position.set(cx, cy, cz);
        mesh.renderOrder = 1;
        mesh.userData.surfaceNumber = surface.surfaceNumber;
        mesh.userData.isHazardMaterial = mat.isHazard;
        mesh.userData.hazardFillType = mat.isHazard ? normalizeStateAppearance(appearance).fillType : null;

        if (!mat.isHazard) {
          mesh.material.polygonOffset = true;
          mesh.material.polygonOffsetFactor = 1;
          mesh.material.polygonOffsetUnits = 1;
        }

        const edgeGeom = new THREE.EdgesGeometry(geom);
        const edges = new THREE.LineSegments(edgeGeom, new THREE.LineBasicMaterial({ color: edgeColor }));
        edges.position.copy(mesh.position);
        edges.renderOrder = 2;
        edges.userData.surfaceNumber = surface.surfaceNumber;

        const depthMat = new THREE.MeshBasicMaterial({
          colorWrite: false,
          depthWrite: true,
        });
        const depthMesh = new THREE.Mesh(geom, depthMat);
        depthMesh.position.set(cx, cy, cz);
        depthMesh.renderOrder = 0;
        depthMesh.userData.isDepthOccluder = true;
        depthMesh.userData.surfaceNumber = surface.surfaceNumber;
        group.add(depthMesh);
        group.add(mesh);
        group.add(edges);
      }

      const bounds = computeBounds(group);
      const labelText = surface.displayNumber || surface.surfaceNumber;
      const stickerOptions = this.viewerOptions?.stickers || {};
      addFaceStickers(group, labelText, bounds, stickerOptions);

      group.visible = !isHidden(surface.surfaceNumber);
      this.root.add(group);
      this.surfaceGroups.set(surface.surfaceNumber, group);
    }

    this.fitView();
  }

  setSurfaceVisible(surfaceNumber, visible) {
    const group = this.surfaceGroups.get(surfaceNumber);
    if (group) group.visible = visible;
  }

  setAllSurfacesVisible(visible) {
    for (const group of this.surfaceGroups.values()) {
      group.visible = visible;
    }
  }

  setSurfaceOpacity(opacity) {
    this.surfaceOpacity = Math.min(1, Math.max(0.25, opacity));
    for (const group of this.surfaceGroups.values()) {
      group.traverse((obj) => {
        if (!obj.isMesh || obj.userData.isSticker || obj.userData.isDepthOccluder) return;
        if (obj.userData.isHazardMaterial) {
          updateHazardMaterialOpacity(obj.material, this.surfaceOpacity);
        } else if (obj.material) {
          applyMeshOpacity(obj.material, this.surfaceOpacity);
        }
      });
    }
  }

  fitView() {
    if (!this.root || !this.camera || !this.controls) return;
    const box = this.getSurfaceBoundsBox();
    if (box.isEmpty()) return;

    const center = box.getCenter(new THREE.Vector3());
    const size = box.getSize(new THREE.Vector3());
    const maxDim = Math.max(size.x, size.y, size.z, 1);
    this.sceneScale = maxDim;

    const dist = maxDim * 1.35;
    this.camera.up.set(0, 1, 0);
    this.camera.near = Math.max(1e-4, maxDim * 1e-4);
    this.camera.far = Math.max(1e4, maxDim * 50);
    this.camera.position.set(center.x + dist * 0.75, center.y + dist * 0.45, center.z + dist * 0.75);
    this.controls.target.copy(center);
    this.camera.lookAt(center);
    this.controls.update();

    this.updateGrid(this.loadedSurfaces);
    this.resize();
  }

  setSurfaceAppearance(surfaceNumber, appearance) {
    const group = this.surfaceGroups.get(surfaceNumber);
    if (!group) return;
    const edgeColor = edgeColorForAppearance(appearance, hexToThreeInt, darkenColorInt);
    group.traverse((obj) => {
      if (obj.isMesh && !obj.userData.isSticker && !obj.userData.isDepthOccluder) {
        syncMeshMaterial(obj, appearance, this.surfaceOpacity);
      }
      if (obj.isLineSegments && obj.material) obj.material.color.setHex(edgeColor);
    });
  }

  setSurfaceColor(surfaceNumber, colorHex) {
    this.setSurfaceAppearance(surfaceNumber, { color: colorHex, fillType: 'solid' });
  }

  setSelection(surfaceNumber) {
    this.selectedSurfaceNumber = surfaceNumber || null;
    for (const [num, group] of this.surfaceGroups.entries()) {
      const selected = num === surfaceNumber;
      group.traverse((obj) => {
        if (!obj.isMesh || obj.userData.isSticker || obj.userData.isDepthOccluder) return;
        if (obj.userData.isHazardMaterial) {
          setHazardMaterialSelection(obj.material, selected);
        } else if (obj.material) {
          obj.material.emissive = new THREE.Color(selected ? 0x224466 : 0x000000);
          obj.material.emissiveIntensity = selected ? 0.35 : 0;
        }
      });
    }
  }

  pickSurfaceAt(event) {
    if (!this.renderer || !this.camera) return null;
    const rect = this.renderer.domElement.getBoundingClientRect();
    this.pointer.x = ((event.clientX - rect.left) / rect.width) * 2 - 1;
    this.pointer.y = -((event.clientY - rect.top) / rect.height) * 2 + 1;
    this.raycaster.setFromCamera(this.pointer, this.camera);
    const meshes = [];
    for (const group of this.surfaceGroups.values()) {
      if (!group.visible) continue;
      group.traverse((obj) => {
        if (obj.isMesh && !obj.userData.isSticker && !obj.userData.isDepthOccluder) meshes.push(obj);
      });
    }
    const hits = this.raycaster.intersectObjects(meshes, false);
    if (hits.length > 0 && hits[0].object.userData.surfaceNumber) {
      return hits[0].object.userData.surfaceNumber;
    }
    return null;
  }

  onPointerDown(event) {
    this.pointerHeldButtons.add(event.button);
    const surfaceNumber = this.pickSurfaceAt(event);
    if (event.button === 2) {
      if (surfaceNumber) {
        const now = Date.now();
        if (
          surfaceNumber === this.lastRightClick.surfaceNumber &&
          now - this.lastRightClick.time < 450
        ) {
          if (typeof this.onSurfaceHide === 'function') {
            this.onSurfaceHide(surfaceNumber);
          }
          this.lastRightClick = { time: 0, surfaceNumber: null };
        } else {
          this.lastRightClick = { time: now, surfaceNumber };
        }
      }
      return;
    }

    if (event.button !== 0 || !surfaceNumber) return;

    const now = Date.now();
    if (
      surfaceNumber === this.lastLeftClick.surfaceNumber &&
      now - this.lastLeftClick.time < 450
    ) {
      if (typeof this.onSurfaceInfoRequest === 'function') {
        this.onSurfaceInfoRequest(surfaceNumber, event.clientX, event.clientY);
      }
      this.lastLeftClick = { time: 0, surfaceNumber: null };
      return;
    }

    this.lastLeftClick = { time: now, surfaceNumber };
    this.setSelection(surfaceNumber);
    if (typeof this.onSurfacePick === 'function') {
      this.onSurfacePick(surfaceNumber);
    }
  }
}
