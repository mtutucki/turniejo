"use client"

import {
  closestCenter,
  DndContext,
  type DragEndEvent,
  KeyboardSensor,
  PointerSensor,
  useSensor,
  useSensors,
} from "@dnd-kit/core"
import {
  arrayMove,
  SortableContext,
  sortableKeyboardCoordinates,
  useSortable,
  verticalListSortingStrategy,
} from "@dnd-kit/sortable"
import { CSS } from "@dnd-kit/utilities"
import { ArrowDown, ArrowUp, GripVertical, Plus, X } from "lucide-react"
import { useEffect, useState } from "react"
import { toast } from "sonner"

import { Button } from "@/components/ui/button"
import { cn } from "@/lib/utils"
import { TIE_BREAKER_LABELS, type TieBreakerRule, type TieBreakerType } from "@/types/tournaments"

import { useSetTieBreakerRules } from "../api"

const ALL_TYPES = Object.keys(TIE_BREAKER_LABELS) as TieBreakerType[]

/** Typowa kolejność w turniejach młodzieżowych — punkt startowy dla organizatora. */
const DEFAULT_RULES: TieBreakerType[] = [
  "HeadToHeadPoints",
  "HeadToHeadGoalDiff",
  "GoalDifference",
  "GoalsFor",
  "Wins",
]

type RuleRowProps = {
  type: TieBreakerType
  index: number
  count: number
  disabled: boolean
  onMove: (from: number, to: number) => void
  onRemove: () => void
}

function RuleRow({ type, index, count, disabled, onMove, onRemove }: RuleRowProps) {
  const { attributes, listeners, setNodeRef, transform, transition, isDragging } = useSortable({
    id: type,
    disabled,
  })

  return (
    <li
      ref={setNodeRef}
      style={{ transform: CSS.Transform.toString(transform), transition }}
      className={cn(
        "bg-card flex items-center gap-2 rounded-lg border px-2 py-2",
        isDragging && "border-primary relative z-10 shadow-lg",
      )}
    >
      <button
        type="button"
        className="text-muted-foreground hover:text-foreground focus-visible:ring-ring cursor-grab touch-none rounded p-1 outline-none focus-visible:ring-2 disabled:cursor-not-allowed disabled:opacity-40"
        aria-label={`Przeciągnij: ${TIE_BREAKER_LABELS[type]}`}
        disabled={disabled}
        {...attributes}
        {...listeners}
      >
        <GripVertical className="size-4" />
      </button>
      <span className="text-primary w-5 font-mono text-sm tabular-nums">{index + 1}.</span>
      <span className="min-w-0 flex-1 text-sm">{TIE_BREAKER_LABELS[type]}</span>
      {!disabled && (
        <span className="flex">
          <Button
            variant="ghost"
            size="icon"
            className="size-8"
            disabled={index === 0}
            onClick={() => onMove(index, index - 1)}
            aria-label={`Przesuń wyżej: ${TIE_BREAKER_LABELS[type]}`}
          >
            <ArrowUp />
          </Button>
          <Button
            variant="ghost"
            size="icon"
            className="size-8"
            disabled={index === count - 1}
            onClick={() => onMove(index, index + 1)}
            aria-label={`Przesuń niżej: ${TIE_BREAKER_LABELS[type]}`}
          >
            <ArrowDown />
          </Button>
          <Button
            variant="ghost"
            size="icon"
            className="size-8"
            onClick={onRemove}
            aria-label={`Usuń regułę: ${TIE_BREAKER_LABELS[type]}`}
          >
            <X />
          </Button>
        </span>
      )}
    </li>
  )
}

type TieBreakerEditorProps = {
  tournamentId: string
  savedRules: TieBreakerRule[]
  canManage: boolean
  /** W kreatorze: po zapisie (albo bez zmian) przejdź do następnego kroku. */
  onSaved?: () => void
  onDirtyChange?: (dirty: boolean) => void
}

/**
 * Kolejność kryteriów rozstrzygania remisów punktowych w tabeli. Zapis podmienia cały zestaw
 * (PUT), kolejność = pozycja na liście.
 */
export function TieBreakerEditor({
  tournamentId,
  savedRules,
  canManage,
  onSaved,
  onDirtyChange,
}: TieBreakerEditorProps) {
  const saved = savedRules.map((r) => r.type)
  const [rules, setRules] = useState<TieBreakerType[]>(saved)
  const setTieBreakerRules = useSetTieBreakerRules(tournamentId)
  const sensors = useSensors(
    useSensor(PointerSensor),
    useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates }),
  )

  const available = ALL_TYPES.filter((type) => !rules.includes(type))
  const isDirty = rules.join() !== saved.join()
  const disabled = !canManage || setTieBreakerRules.isPending

  useEffect(() => {
    onDirtyChange?.(isDirty)
  }, [isDirty, onDirtyChange])

  function move(from: number, to: number) {
    setRules((current) => arrayMove(current, from, to))
  }

  function onDragEnd({ active, over }: DragEndEvent) {
    if (!over || active.id === over.id) return
    setRules((current) =>
      arrayMove(
        current,
        current.indexOf(active.id as TieBreakerType),
        current.indexOf(over.id as TieBreakerType),
      ),
    )
  }

  function save() {
    if (!isDirty) return onSaved?.()
    setTieBreakerRules.mutate(rules, {
      onSuccess: () => {
        toast.success("Kolejność reguł zapisana")
        onSaved?.()
      },
    })
  }

  return (
    <div className="space-y-4">
      {rules.length === 0 ? (
        <p className="text-muted-foreground text-sm">
          Brak reguł - przy równej liczbie punktów kolejność w tabeli nie będzie rozstrzygana.
        </p>
      ) : (
        <DndContext sensors={sensors} collisionDetection={closestCenter} onDragEnd={onDragEnd}>
          <SortableContext items={rules} strategy={verticalListSortingStrategy}>
            <ol className="space-y-2" aria-label="Kolejność reguł">
              {rules.map((type, index) => (
                <RuleRow
                  key={type}
                  type={type}
                  index={index}
                  count={rules.length}
                  disabled={disabled}
                  onMove={move}
                  onRemove={() => setRules((current) => current.filter((t) => t !== type))}
                />
              ))}
            </ol>
          </SortableContext>
        </DndContext>
      )}

      {canManage && available.length > 0 && (
        <div className="space-y-2">
          <p className="text-muted-foreground text-xs font-medium tracking-wide uppercase">
            Dodaj regułę
          </p>
          <div className="flex flex-wrap gap-2">
            {available.map((type) => (
              <Button
                key={type}
                variant="outline"
                size="sm"
                disabled={disabled}
                onClick={() => setRules((current) => [...current, type])}
              >
                <Plus />
                {TIE_BREAKER_LABELS[type]}
              </Button>
            ))}
          </div>
        </div>
      )}

      {canManage && (
        <div className="flex flex-wrap gap-2">
          <Button
            onClick={save}
            disabled={disabled || (!isDirty && !onSaved) || rules.length === 0}
          >
            {setTieBreakerRules.isPending
              ? "Zapisywanie…"
              : onSaved
                ? "Zapisz i dalej"
                : "Zapisz kolejność"}
          </Button>
          {isDirty && (
            <Button variant="ghost" onClick={() => setRules(saved)} disabled={disabled}>
              Odrzuć zmiany
            </Button>
          )}
          {rules.join() !== DEFAULT_RULES.join() && (
            <Button variant="ghost" onClick={() => setRules(DEFAULT_RULES)} disabled={disabled}>
              Wstaw domyślną kolejność
            </Button>
          )}
        </div>
      )}
    </div>
  )
}
