import { Component, signal, viewChild } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { PaginatorComponent } from './paginator.component';

@Component({
  imports: [PaginatorComponent],
  template: `<app-paginator
    [totalRecords]="totalRecords()"
    [pageSize]="pageSize()"
    [currentPage]="currentPage()"
    (pageChange)="onPageChange($event)"
    (pageSizeChange)="onPageSizeChange($event)"
  />`,
})
class HostComponent {
  readonly paginator = viewChild.required(PaginatorComponent);
  readonly totalRecords = signal(100);
  readonly pageSize = signal(50);
  readonly currentPage = signal(1);
  readonly pages: number[] = [];
  readonly pageSizes: number[] = [];

  onPageChange(page: number): void {
    this.pages.push(page);
  }

  onPageSizeChange(size: number): void {
    this.pageSizes.push(size);
  }
}

describe('PaginatorComponent', () => {
  function setup(): ComponentFixture<HostComponent> {
    const fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
    return fixture;
  }

  it('reports one page and no navigation when there are no records', () => {
    const fixture = TestBed.createComponent(PaginatorComponent);
    fixture.componentRef.setInput('totalRecords', 0);
    fixture.detectChanges();

    const paginator = fixture.componentInstance;

    expect(paginator.totalPages()).toBe(1);
    expect(paginator.canGoPrevious()).toBe(false);
    expect(paginator.canGoNext()).toBe(false);
    expect(paginator.displayRange()).toEqual({ start: 0, end: 0 });
  });

  it('emits pageChange only for pages inside the valid range', () => {
    const fixture = setup();
    const host = fixture.componentInstance;
    const paginator = host.paginator();

    paginator.goToPage(0);
    paginator.goToPage(3);
    fixture.detectChanges();

    expect(host.pages).toEqual([]);

    paginator.goToPage(2);
    fixture.detectChanges();

    expect(host.pages).toEqual([2]);
  });

  it('ignores a non-numeric page size and one equal to the current size', () => {
    const fixture = setup();
    const host = fixture.componentInstance;
    const paginator = host.paginator();

    paginator.onPageSizeChange('100');
    paginator.onPageSizeChange(null);
    paginator.onPageSizeChange(50);
    fixture.detectChanges();

    expect(host.pageSizes).toEqual([]);

    paginator.onPageSizeChange(100);
    fixture.detectChanges();

    expect(host.pageSizes).toEqual([100]);
  });
});
