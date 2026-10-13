#pragma once

namespace spidey {
namespace core {

// Save the next link before invoking the visitor. The visitor may destroy or
// move the current node, or insert nodes at the head; those new nodes wait for
// the next pass. It must not destroy the saved next node during this pass.
template<typename Node, typename NextNode, typename Visitor>
void visitIntrusiveList(Node* first, NextNode nextNode, Visitor&& visitor) {
    Node* current = first;
    while (current) {
        Node* next = nextNode(*current);
        visitor(*current);
        current = next;
    }
}

} // namespace core
} // namespace spidey
