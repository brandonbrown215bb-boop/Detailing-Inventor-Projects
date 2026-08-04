export const FILL_SOLID = 'solid';
export const FILL_HAZARD_YB = 'hazard-yellow-black';
export const FILL_HAZARD_RB = 'hazard-red-black';

export const FILL_TYPE_OPTIONS = [
  { id: FILL_SOLID, label: 'Solid color' },
  { id: FILL_HAZARD_YB, label: 'Yellow / black hazard' },
  { id: FILL_HAZARD_RB, label: 'Red / black hazard' },
];

const WRAP_COLORS = {
  [FILL_HAZARD_YB]: ['#facc15', '#111827'],
  [FILL_HAZARD_RB]: ['#dc2626', '#111827'],
};

/** Stripe width in world units (inches). */
export const STRIPE_PERIOD = 5;

// World-space unlit stripes — continuous across faces and adjacent boxes (no UV tiling seams).
const HAZARD_VERT = /* glsl */ `
#include <logdepthbuf_pars_vertex>

varying vec3 vWorldPos;

void main() {
  vWorldPos = (modelMatrix * vec4(position, 1.0)).xyz;
  gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
  #include <logdepthbuf_vertex>
}
`;

const HAZARD_FRAG = /* glsl */ `
#include <logdepthbuf_pars_fragment>

varying vec3 vWorldPos;

uniform vec3 uColorA;
uniform vec3 uColorB;
uniform float uStripePeriod;
uniform float uOpacity;
uniform vec3 uEmissive;
uniform float uEmissiveIntensity;

void main() {
  // 45° hazard bands in plan view; shared world coords keep stripes aligned everywhere.
  float diag = vWorldPos.x + vWorldPos.y;
  float stripe = mod(floor(diag / uStripePeriod), 2.0);
  vec3 color = mix(uColorA, uColorB, stripe);
  color += uEmissive * uEmissiveIntensity;
  gl_FragColor = vec4(color, uOpacity);
  #include <logdepthbuf_fragment>
}
`;

export function normalizeStateAppearance(state) {
  const fillType = state?.fillType || FILL_SOLID;
  const validFill = FILL_TYPE_OPTIONS.some((o) => o.id === fillType) ? fillType : FILL_SOLID;
  return {
    color: state?.color || '#94a3b8',
    fillType: validFill,
  };
}

export function getSurfaceAppearanceFromState(state) {
  return normalizeStateAppearance(state);
}

export function isSolidFill(fillType) {
  return !fillType || fillType === FILL_SOLID;
}

export function getSwatchBackground(state) {
  const { color, fillType } = normalizeStateAppearance(state);
  if (fillType === FILL_HAZARD_YB) {
    return {
      backgroundColor: '#111827',
      backgroundImage: 'repeating-linear-gradient(45deg, #facc15 0 5px, #111827 5px 10px)',
    };
  }
  if (fillType === FILL_HAZARD_RB) {
    return {
      backgroundColor: '#111827',
      backgroundImage: 'repeating-linear-gradient(45deg, #dc2626 0 5px, #111827 5px 10px)',
    };
  }
  return { backgroundColor: color, backgroundImage: '' };
}

export function createHazardMaterial(fillType, THREE, opacity = 0.9) {
  const colors = WRAP_COLORS[fillType];
  if (!colors) return null;

  const value = Math.min(1, Math.max(0.25, opacity));
  const opaque = value >= 0.999;

  const mat = new THREE.ShaderMaterial({
    uniforms: {
      uStripePeriod: { value: STRIPE_PERIOD },
      uColorA: { value: new THREE.Color(colors[0]) },
      uColorB: { value: new THREE.Color(colors[1]) },
      uOpacity: { value: value },
      uEmissive: { value: new THREE.Color(0x000000) },
      uEmissiveIntensity: { value: 0 },
    },
    vertexShader: HAZARD_VERT,
    fragmentShader: HAZARD_FRAG,
    side: THREE.DoubleSide,
    transparent: !opaque,
    depthWrite: opaque,
    depthTest: true,
    toneMapped: false,
  });
  mat.userData.hazardFillType = fillType;
  return mat;
}

export function updateHazardMaterialOpacity(material, opacity) {
  if (!material?.uniforms?.uOpacity) return;
  const value = Math.min(1, Math.max(0.25, opacity));
  const opaque = value >= 0.999;
  material.uniforms.uOpacity.value = value;
  material.transparent = !opaque;
  material.depthWrite = opaque;
  material.needsUpdate = true;
}

export function setHazardMaterialSelection(material, selected) {
  if (!material?.uniforms) return;
  material.uniforms.uEmissive.value.setHex(selected ? 0x224466 : 0x000000);
  material.uniforms.uEmissiveIntensity.value = selected ? 0.35 : 0;
}

export function edgeColorForAppearance(appearance, hexToThreeInt, darkenColorInt) {
  const { color, fillType } = normalizeStateAppearance(appearance);
  if (fillType === FILL_HAZARD_YB) return hexToThreeInt('#ca8a04');
  if (fillType === FILL_HAZARD_RB) return hexToThreeInt('#991b1b');
  return darkenColorInt(hexToThreeInt(color), 0.55);
}
