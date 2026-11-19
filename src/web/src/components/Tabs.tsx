import { useId, useState, type KeyboardEvent, type ReactNode } from 'react'

export interface Tab {
  label: string
  content: ReactNode
}

// Only the selected tab's content is rendered, so a tab's queries run when it opens.
function Tabs({ label, tabs }: { label: string; tabs: Tab[] }) {
  const [selected, setSelected] = useState(0)
  const id = useId()

  function onKeyDown(event: KeyboardEvent<HTMLDivElement>) {
    const step = event.key === 'ArrowRight' ? 1 : event.key === 'ArrowLeft' ? -1 : 0
    if (step === 0) return
    const next = (selected + step + tabs.length) % tabs.length
    setSelected(next)
    document.getElementById(`${id}-tab-${next}`)?.focus()
  }

  return (
    <>
      <div role="tablist" aria-label={label} className="tabs" onKeyDown={onKeyDown}>
        {tabs.map((tab, index) => (
          <button
            key={tab.label}
            id={`${id}-tab-${index}`}
            type="button"
            role="tab"
            aria-selected={index === selected}
            aria-controls={`${id}-panel`}
            tabIndex={index === selected ? 0 : -1}
            onClick={() => setSelected(index)}
          >
            {tab.label}
          </button>
        ))}
      </div>
      <div id={`${id}-panel`} role="tabpanel" aria-labelledby={`${id}-tab-${selected}`}>
        {tabs[selected]?.content}
      </div>
    </>
  )
}

export default Tabs
