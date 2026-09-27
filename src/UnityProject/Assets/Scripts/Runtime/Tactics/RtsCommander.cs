using System.Collections.Generic;
using Pacifico.Core.Common;
using Pacifico.Core.Tactics;
using Pacifico.Input;
using UnityEngine;

namespace Pacifico.Tactics
{
    /// <summary>
    /// Mando del jugador en la fase RTS (ROADMAP 4.1).
    /// <list type="bullet">
    /// <item>Clic izquierdo o recuadro: seleccionar escuadras (Mayús añade, Ctrl alterna).</item>
    /// <item>Clic derecho en el suelo: mover; arrastrando, el trazo marca el frente y la orientación.</item>
    /// <item>Clic derecho sobre un enemigo: atacar.</item>
    /// <item>1 / 2: formar en línea o en guerrilla. H: alto.</item>
    /// </list>
    /// La lógica de selección y de reparto del frente está en <see cref="SelectionLogic"/> y
    /// <see cref="OrderPlanner"/>, probadas fuera del motor; aquí solo se leen el ratón y el teclado y se dibuja.
    /// </summary>
    public sealed class RtsCommander : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [SerializeField] private LayerMask groundMask = Physics.DefaultRaycastLayers;

        private readonly HashSet<int> _selection = new HashSet<int>();
        private readonly List<ScreenPoint> _screenPoints = new List<ScreenPoint>();
        private readonly List<SquadController> _selected = new List<SquadController>();
        private readonly List<SquadFootprint> _footprints = new List<SquadFootprint>();
        private readonly List<SquadController> _own = new List<SquadController>();

        private bool _selecting;
        private Vector2 _selectStart;
        private bool _ordering;
        private Vector3 _orderStart;
        private Vector3 _orderEnd;
        private SquadController _attackCandidate;
        private SquadDestination[] _preview;
        private SquadController _hoverEnemy;

        private Texture2D _white;
        private GUIStyle _label;
        private GUIStyle _small;

        public Camera ViewCamera
        {
            get => viewCamera;
            set => viewCamera = value;
        }

        /// <summary>Escuadras seleccionadas (vivas), en el orden de la lista de la escena.</summary>
        public IReadOnlyList<SquadController> Selected
        {
            get
            {
                _selected.Clear();
                foreach (SquadController s in SquadController.All)
                {
                    if (s.PlayerControlled && s.IsAlive && _selection.Contains(s.GetInstanceID())) _selected.Add(s);
                }
                return _selected;
            }
        }

        private void Update()
        {
            if (viewCamera == null) viewCamera = Camera.main;
            if (viewCamera == null) return;
            Vector2 mouse = GameInput.MousePosition;
            _hoverEnemy = EnemyUnderCursor(mouse);

            // --- Selección -------------------------------------------------------------------------
            if (GameInput.MousePressed(0))
            {
                _selecting = true;
                _selectStart = mouse;
            }
            if (_selecting && GameInput.MouseReleased(0))
            {
                _selecting = false;
                ScreenRect rect = ScreenRect.FromCorners(_selectStart.x, _selectStart.y, mouse.x, mouse.y);
                CollectScreenPoints();
                SelectionMode mode = GameInput.Held(GameKey.LeftShift) ? SelectionMode.Add
                    : GameInput.Held(GameKey.LeftControl) ? SelectionMode.Toggle
                    : SelectionMode.Replace;
                SelectionLogic.Apply(_selection, SelectionLogic.Pick(rect, _screenPoints), mode);
            }

            // --- Órdenes ---------------------------------------------------------------------------
            IReadOnlyList<SquadController> selected = Selected;
            if (GameInput.MousePressed(1) && selected.Count > 0 && GroundPoint(mouse, out _orderStart))
            {
                _ordering = true;
                _orderEnd = _orderStart;
                _attackCandidate = _hoverEnemy;
            }
            if (_ordering)
            {
                if (GroundPoint(mouse, out Vector3 end)) _orderEnd = end;
                bool dragged = Vector3.Distance(_orderStart, _orderEnd) >= OrderPlanner.DragThresholdM;
                _preview = _attackCandidate != null && !dragged ? null : Plan(selected);
                if (GameInput.MouseReleased(1))
                {
                    _ordering = false;
                    if (_attackCandidate != null && !dragged)
                    {
                        foreach (SquadController s in selected) s.IssueAttack(_attackCandidate);
                    }
                    else if (_preview != null)
                    {
                        for (int i = 0; i < selected.Count; i++) selected[i].IssueMove(ToVector3(_preview[i].Anchor), ToVector3(_preview[i].Facing));
                    }
                    _preview = null;
                    _attackCandidate = null;
                }
            }

            if (GameInput.Pressed(GameKey.Digit1)) foreach (SquadController s in selected) s.SetFormation(FormationType.Line);
            if (GameInput.Pressed(GameKey.Digit2)) foreach (SquadController s in selected) s.SetFormation(FormationType.Skirmish);
            if (GameInput.Pressed(GameKey.H)) foreach (SquadController s in selected) s.IssueHalt();
        }

        private SquadDestination[] Plan(IReadOnlyList<SquadController> squads)
        {
            _footprints.Clear();
            foreach (SquadController s in squads) _footprints.Add(s.Footprint);
            return OrderPlanner.PlanMove(_footprints, ToVec3(_orderStart), ToVec3(_orderEnd));
        }

        private void CollectScreenPoints()
        {
            _screenPoints.Clear();
            foreach (SquadController squad in SquadController.All)
            {
                if (!squad.PlayerControlled || !squad.IsAlive) continue;
                int id = squad.GetInstanceID();
                foreach (SoldierUnit soldier in squad.Soldiers)
                {
                    Vector3 p = viewCamera.WorldToScreenPoint(soldier.transform.position + Vector3.up);
                    _screenPoints.Add(new ScreenPoint(id, p.x, p.y, p.z > 0f));
                }
            }
        }

        private bool GroundPoint(Vector2 screen, out Vector3 point)
        {
            Ray ray = viewCamera.ScreenPointToRay(screen);
            if (Physics.Raycast(ray, out RaycastHit hit, 3000f, groundMask, QueryTriggerInteraction.Ignore))
            {
                point = hit.point;
                return true;
            }
            point = default;
            return false;
        }

        private SquadController EnemyUnderCursor(Vector2 screen)
        {
            Ray ray = viewCamera.ScreenPointToRay(screen);
            // Los soldados están en «Ignore Raycast»: se buscan con una máscara que solo incluye esa capa.
            if (!Physics.SphereCast(ray, 0.6f, out RaycastHit hit, 3000f, 1 << SoldierUnit.Layer, QueryTriggerInteraction.Ignore)) return null;
            SoldierUnit soldier = hit.collider.GetComponentInParent<SoldierUnit>();
            if (soldier == null || soldier.Squad == null || soldier.Squad.PlayerControlled) return null;
            return soldier.Squad;
        }

        // ------------------------------------------------------------------------------------------
        // Dibujo (IMGUI, sin assets)
        // ------------------------------------------------------------------------------------------

        private void OnGUI()
        {
            if (viewCamera == null) return;
            EnsureStyles();

            // Marcas bajo los soldados seleccionados y nombre de la escuadra.
            foreach (SquadController squad in Selected)
            {
                foreach (SoldierUnit soldier in squad.Soldiers) Mark(soldier.transform.position, new Color(0.55f, 0.95f, 0.55f, 0.9f), 6f);
                Label(squad.CenterOfMass() + Vector3.up * 3f, squad.DisplayName, _small);
            }
            foreach (SquadController squad in SquadController.All)
            {
                if (squad.Suppression.State == SuppressionState.Normal) continue;
                Label(squad.CenterOfMass() + Vector3.up * 5f, squad.Suppression.State == SuppressionState.Suppressed ? "¡Suprimida!" : "Presionada", _small);
            }
            if (_hoverEnemy != null)
            {
                foreach (SoldierUnit soldier in _hoverEnemy.Soldiers) Mark(soldier.transform.position, new Color(1f, 0.35f, 0.25f, 0.9f), 6f);
                Label(_hoverEnemy.CenterOfMass() + Vector3.up * 3f, _hoverEnemy.DisplayName + " — [clic derecho] atacar", _small);
            }

            // Previsualización de la orden: los puestos finales de cada hombre.
            if (_ordering && _preview != null)
            {
                IReadOnlyList<SquadController> selected = Selected;
                for (int i = 0; i < selected.Count && i < _preview.Length; i++)
                {
                    Vec3[] local = Formation.LocalSlots(selected[i].Formation, selected[i].Strength);
                    foreach (Vec3 l in local)
                    {
                        Vector3 w = ToVector3(Formation.ToWorld(_preview[i].Anchor, _preview[i].Facing, l));
                        Mark(w, new Color(1f, 1f, 1f, 0.7f), 4f);
                    }
                    Mark(ToVector3(_preview[i].Anchor + _preview[i].Facing * 4f), new Color(1f, 0.9f, 0.4f, 0.9f), 7f);
                }
            }

            // Recuadro de selección.
            if (_selecting)
            {
                Vector2 mouse = GameInput.MousePosition;
                var rect = Rect.MinMaxRect(Mathf.Min(_selectStart.x, mouse.x), Screen.height - Mathf.Max(_selectStart.y, mouse.y),
                                           Mathf.Max(_selectStart.x, mouse.x), Screen.height - Mathf.Min(_selectStart.y, mouse.y));
                GUI.color = new Color(0.55f, 0.95f, 0.55f, 0.15f);
                GUI.DrawTexture(rect, _white);
                GUI.color = new Color(0.55f, 0.95f, 0.55f, 0.9f);
                GUI.DrawTexture(new Rect(rect.xMin, rect.yMin, rect.width, 1f), _white);
                GUI.DrawTexture(new Rect(rect.xMin, rect.yMax - 1f, rect.width, 1f), _white);
                GUI.DrawTexture(new Rect(rect.xMin, rect.yMin, 1f, rect.height), _white);
                GUI.DrawTexture(new Rect(rect.xMax - 1f, rect.yMin, 1f, rect.height), _white);
                GUI.color = Color.white;
            }

            DrawPanel();
        }

        private void DrawPanel()
        {
            float y = Screen.height - 24f;
            List<SquadController> own = _own;
            own.Clear();
            foreach (SquadController s in SquadController.All) if (s.PlayerControlled) own.Add(s);
            for (int i = own.Count - 1; i >= 0; i--)
            {
                SquadController s = own[i];
                bool selected = _selection.Contains(s.GetInstanceID());
                string state = s.Order == SquadOrderKind.Attack ? "Atacando" : s.Command.March.Moving ? "En marcha" : s.Target != null ? "Haciendo fuego" : "Alto";
                string text = (selected ? "> " : "   ") + s.DisplayName + " — " + s.Strength + "/" + s.InitialStrength + " · " +
                              (s.Formation == FormationType.Line ? "Línea" : "Guerrilla") + " · " + state +
                              " · " + SuppressionName(s.Suppression.State) + (s.InCover > 0 ? " · a cubierto " + s.InCover : string.Empty) +
                              " · bajas causadas: " + s.EnemyCasualties;
                DrawSuppressionBar(new Rect(12f, y - 3f, 120f, 3f), s.Suppression.Level);
                GUI.Label(new Rect(12f, y - 20f, 900f, 22f), text, _label);
                y -= 22f;
            }
            GUI.Label(new Rect(12f, 10f, 1200f, 22f),
                "Clic/recuadro: seleccionar (Mayús añade, Ctrl alterna) · Clic dcho.: mover (arrastrar: frente y orientación) · " +
                "Clic dcho. sobre enemigo: atacar · 1 línea · 2 guerrilla · H alto · WASD/QE/rueda: cámara", _small);
        }

        public static string SuppressionName(SuppressionState state)
        {
            switch (state)
            {
                case SuppressionState.Pinned: return "Presionada";
                case SuppressionState.Suppressed: return "SUPRIMIDA";
                default: return "Serena";
            }
        }

        /// <summary>Barra de supresión: verde por debajo de «presionada», ámbar hasta «suprimida», roja por encima.</summary>
        private void DrawSuppressionBar(Rect rect, float level)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.45f);
            GUI.DrawTexture(rect, _white);
            float fill = Mathf.Clamp01(level);
            GUI.color = level >= SuppressionModel.SuppressEnter ? new Color(0.9f, 0.25f, 0.2f)
                : level >= SuppressionModel.PinEnter ? new Color(0.95f, 0.7f, 0.2f)
                : new Color(0.5f, 0.85f, 0.5f);
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * fill, rect.height), _white);
            GUI.color = Color.white;
        }

        private void Mark(Vector3 world, Color color, float size)
        {
            Vector3 p = viewCamera.WorldToScreenPoint(world);
            if (p.z <= 0f) return;
            GUI.color = color;
            GUI.DrawTexture(new Rect(p.x - size * 0.5f, Screen.height - p.y - size * 0.5f, size, size), _white);
            GUI.color = Color.white;
        }

        private void Label(Vector3 world, string text, GUIStyle style)
        {
            Vector3 p = viewCamera.WorldToScreenPoint(world);
            if (p.z <= 0f) return;
            GUI.Label(new Rect(p.x - 150f, Screen.height - p.y - 11f, 300f, 22f), text, style);
        }

        private void EnsureStyles()
        {
            if (_label != null) return;
            _white = Texture2D.whiteTexture;
            _label = new GUIStyle(GUI.skin.label) { fontSize = 15 };
            _label.normal.textColor = new Color(0.97f, 0.94f, 0.86f);
            _small = new GUIStyle(_label) { fontSize = 13, alignment = TextAnchor.MiddleCenter };
        }

        private static Vec3 ToVec3(Vector3 v) => new Vec3(v.x, v.y, v.z);

        private static Vector3 ToVector3(Vec3 v) => new Vector3(v.X, v.Y, v.Z);
    }
}
