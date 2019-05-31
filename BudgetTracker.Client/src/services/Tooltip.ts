import { writable } from 'svelte/store';

export let text = writable("");
export let show = writable(false);
export let position = writable({left: 0, top: 0, width: 0, height: 0});

export function tooltip(node: any, newText: string) {
    let mouseover = () => {
    position.set(node.getBoundingClientRect());
    text.set(newText);
    show.set(true);
    }
    
    let mouseout = () => show.set(false);

    node.addEventListener('mouseover', mouseover);
    node.addEventListener('mouseout', mouseout);

    return {
    destroy() {
        node.removeEventListener('mouseover', mouseover);
        node.removeEventListener('mouseout', mouseout);
    }
    };
}