import { fireEvent, render, screen } from '@testing-library/react'
import { expect, test, vi } from 'vitest'
import MapView from './MapView.tsx'

const state = {
  start: { x: 0.1, y: 0.1 },
  waypoints: [
    { x: 0.2, y: 0.3, label: 'The crossroads' },
    { x: 0.5, y: 0.5, label: '' },
  ],
  earlier: [
    [
      { x: 0, y: 0 },
      { x: 0.1, y: 0.1 },
    ],
  ],
  party: { x: 0.5, y: 0.5 },
}

test('shows the map image, numbered stops and where the party is', () => {
  const { container } = render(<MapView imageUrl="/map.png" state={state} />)

  expect(container.querySelector('img')).toHaveAttribute('src', '/map.png')
  expect(screen.getByLabelText('Stop 1: The crossroads')).toBeInTheDocument()
  expect(screen.getByLabelText('Stop 2')).toBeInTheDocument()
  expect(screen.getByLabelText('The party is here')).toBeInTheDocument()
  expect(container.querySelectorAll('.route-earlier')).toHaveLength(1)
  expect(container.querySelectorAll('.route-current')).toHaveLength(1)
})

test('the current route starts at the previous session’s end', () => {
  const { container } = render(<MapView imageUrl="/map.png" state={state} />)

  const points = container.querySelector('.route-current')?.getAttribute('points')
  // The default view box is 1000 wide and 750 tall until the image loads.
  expect(points).toBe('100,75 200,225 500,375')
})

test('with nothing travelled there is no route or party marker', () => {
  const { container } = render(
    <MapView imageUrl="/map.png" state={{ start: null, waypoints: [], earlier: [], party: null }} />,
  )

  expect(container.querySelector('.route-current')).toBeNull()
  expect(screen.queryByLabelText('The party is here')).not.toBeInTheDocument()
})

test('clicking the map reports the position as fractions of its size', () => {
  const onMapClick = vi.fn()
  render(<MapView imageUrl="/map.png" state={state} onMapClick={onMapClick} />)
  const map = screen.getByLabelText('Region map')
  map.getBoundingClientRect = () =>
    ({ left: 10, top: 20, width: 200, height: 100, right: 210, bottom: 120, x: 10, y: 20 }) as DOMRect

  fireEvent.click(map, { clientX: 60, clientY: 95 })

  expect(onMapClick).toHaveBeenCalledWith({ x: 0.25, y: 0.75 })
})

test('clicks are kept inside the map', () => {
  const onMapClick = vi.fn()
  render(<MapView imageUrl="/map.png" state={state} onMapClick={onMapClick} />)
  const map = screen.getByLabelText('Region map')
  map.getBoundingClientRect = () =>
    ({ left: 0, top: 0, width: 100, height: 100, right: 100, bottom: 100, x: 0, y: 0 }) as DOMRect

  fireEvent.click(map, { clientX: 120, clientY: -5 })

  expect(onMapClick).toHaveBeenCalledWith({ x: 1, y: 0 })
})
